namespace AJut.Text
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Text;
    using AJut.Storage;

    /// <summary>
    /// A boolean expression over terms, like <c>code &amp; (frontend | ui)</c> or <c>Jill Spill | (Ghost &amp; !Pilly)</c>.
    /// Parse it once with <see cref="Parse"/>, then test it as often as you like:
    /// <see cref="Evaluate(IReadOnlyCollection{string}, TermMatchOptions)"/> checks it against a set of terms (tags,
    /// categories, or flags), <see cref="IsMatch(string, TermMatchOptions)"/> searches text for its terms (a search box
    /// filtering records), and <see cref="Evaluate(Func{string, bool})"/> lets you decide what each term means.
    /// </summary>
    /// <remarks>
    /// <para>The grammar:</para>
    /// <list type="bullet">
    /// <item>A term is everything between operators and parentheses, with its ends trimmed, so <c>Mr Toot | Tommy Boy</c> is
    /// the two terms "Mr Toot" and "Tommy Boy". It can hold anything but <c>&amp;</c>, <c>|</c>, parentheses, <c>"</c>, and
    /// <c>!</c>, and needs at least one letter or digit. Whitespace inside it collapses to one space unless
    /// <see cref="TermParseOptions.InnerWhitespace"/> says to keep it.</item>
    /// <item>Quote a term to put anything else in it, <c>"This &amp; That"</c>, using <c>\"</c> and <c>\\</c> for a quote or a
    /// backslash inside the quotes. A quoted term keeps its whitespace exactly.</item>
    /// <item><c>&amp;</c> needs both sides, <c>|</c> needs either, <c>!</c> in front of a term or a group flips it, and
    /// parentheses group. <c>&amp;&amp;</c> and <c>||</c> work too, for anybody used to writing them, and come back out as
    /// <c>&amp;</c> and <c>|</c>.</item>
    /// <item><c>&amp;</c> and <c>|</c> can't be mixed at one level without parentheses, so nobody reading an expression has to
    /// know which one binds first. <c>a &amp; b | c</c> is refused, and the refusal shows both ways to write it.</item>
    /// </list>
    /// <para>A <see cref="TermExpression"/> never changes once parsed, so one instance can be tested from any number of threads
    /// at once.</para>
    /// </remarks>
    public sealed partial class TermExpression
    {
        // What one evaluation knows about each distinct term while it runs. Not asked yet has to be zero, since the answers
        //  start out as a zeroed buffer.
        private const byte kNotAskedYet = 0;
        private const byte kAbsent = 1;
        private const byte kPresent = 2;

        private readonly Node[] m_nodes;
        private readonly string[] m_terms;
        private readonly bool m_hasRepeatedTerms;
        private string m_canonicalText;

        // ===========[ Setup/Construction/Teardown ]==========================

        private TermExpression (Node[] nodes, string[] terms)
        {
            m_nodes = nodes;
            m_terms = terms;
            this.Terms = Array.AsReadOnly(terms);

            int termNodeCount = 0;
            foreach (Node node in nodes)
            {
                if (node.Kind == eNodeKind.Term)
                {
                    ++termNodeCount;
                }
            }

            m_hasRepeatedTerms = termNodeCount > terms.Length;
        }

        // ===========[ Properties ]===========================================

        /// <summary>
        /// Every term the expression names, each exact spelling once, in the order they first appear. Check them against the
        /// terms you know before accepting an expression somebody typed.
        /// </summary>
        public IReadOnlyList<string> Terms { get; }

        // ===========[ Public Interface Methods ]=============================

        /// <summary>
        /// Evaluates the expression with each term counting as present when <paramref name="terms"/> holds it.
        /// </summary>
        /// <remarks>
        /// A <see cref="HashSet{T}"/> that already compares the way <paramref name="options"/> asks (built with
        /// <see cref="StringComparer.OrdinalIgnoreCase"/> by default, or with <see cref="StringComparer.Ordinal"/> or no comparer
        /// when matching case) answers each term with one lookup. Anything else gets scanned once for each term the evaluation
        /// reaches, which is quick for the handful of terms something usually carries. If you're testing against a big
        /// collection over and over, hand it in as a matching set.
        /// </remarks>
        public bool Evaluate (IReadOnlyCollection<string> terms, TermMatchOptions options = default)
        {
            ArgumentNullException.ThrowIfNull(terms);

            // An array gets read directly, since going through its collection interfaces costs a call per element
            if (terms is string[] array)
            {
                var arrayPresence = new ArrayPresence(array, options);
                return this.EvaluateWith(ref arrayPresence);
            }

            var presence = new CollectionPresence(terms, options);
            return this.EvaluateWith(ref presence);
        }

        /// <summary>
        /// Evaluates the expression with each term counting as present when it's one of <paramref name="terms"/>, ignoring case.
        /// </summary>
        public bool Evaluate (params string[] terms)
        {
            ArgumentNullException.ThrowIfNull(terms);

            var presence = new ArrayPresence(terms, default);
            return this.EvaluateWith(ref presence);
        }

        /// <summary>
        /// Evaluates the expression with <paramref name="isPresent"/> deciding whether each term counts as present: it's handed
        /// a term's text and answers true or false, and what a term means is up to it. That's for when a collection or a text
        /// isn't the right fit. It's called at most once per distinct term, and only for the terms the evaluation actually
        /// reaches.
        /// </summary>
        /// <example>
        /// A flag check that isn't a plain collection, where <c>flags.IsEnabled</c> takes a flag's name and says whether it's
        /// switched on right now:
        /// <code>
        /// TermExpression rule = TermExpression.Parse("new-editor &amp; !safe-mode").Value;
        /// bool showNewEditor = rule.Evaluate(flags.IsEnabled);
        /// </code>
        /// Or a kind of term of your own. ':' and '>' aren't reserved, so <c>class:bard &amp; (level>3 | name:Jill Spill)</c>
        /// is the three terms "class:bard", "level>3", and "name:Jill Spill", and a function of yours (<c>Has</c> here) reads
        /// each one and checks it against the record in hand:
        /// <code>
        /// List&lt;Character&gt; found = party.Where(character => query.Evaluate(term => Has(character, term))).ToList();
        /// </code>
        /// </example>
        public bool Evaluate (Func<string, bool> isPresent)
        {
            ArgumentNullException.ThrowIfNull(isPresent);

            var presence = new CallerPresence(isPresent);
            return this.EvaluateWith(ref presence);
        }

        /// <summary>
        /// Searches <paramref name="text"/> for the expression's terms, and evaluates the expression with each term counting as
        /// present when it's found. A null text holds nothing.
        /// </summary>
        /// <remarks>
        /// Each term the evaluation reaches is searched for once, with one scan of the text that stops at its first match (or
        /// its first whole word match). So a check costs about the number of distinct terms times the length of the text.
        /// That's quick for filtering what you've already got in memory, even thousands of records or a few long documents on
        /// every keystroke. It isn't an index though. Once the text you're searching is big enough that scanning it once per
        /// term is too slow, you want a search engine, not this.
        /// </remarks>
        public bool IsMatch (string text, TermMatchOptions options = default)
        {
            var presence = new TextPresence(text, options);
            return this.EvaluateWith(ref presence);
        }

        /// <summary>
        /// Searches all of <paramref name="texts"/> for the expression's terms, and evaluates the expression with each term
        /// counting as present when it's found in any of them. That lets you search several fields of a record as one:
        /// <c>records.Where(r => expression.IsMatch(r.Name, r.Body))</c>. Null texts hold nothing.
        /// </summary>
        /// <remarks>
        /// The cost is the same as searching one text (see <see cref="IsMatch(string, TermMatchOptions)"/>), with the
        /// length of all the texts together standing in for the length of one.
        /// </remarks>
        public bool IsMatch (IReadOnlyList<string> texts, TermMatchOptions options = default)
        {
            ArgumentNullException.ThrowIfNull(texts);

            var presence = new TextPresence(texts, options);
            return this.EvaluateWith(ref presence);
        }

        /// <summary>
        /// Searches all of <paramref name="texts"/> for the expression's terms, ignoring case and finding them anywhere, and
        /// evaluates the expression with each term counting as present when it's found in any of them.
        /// </summary>
        /// <remarks>
        /// Like any params call, this builds a new array of the texts every time it's called. That's usually nothing, but if
        /// you're filtering a long list on every keystroke, fill one array of fields per record and pass it to
        /// <see cref="IsMatch(IReadOnlyList{string}, TermMatchOptions)"/> instead.
        /// </remarks>
        public bool IsMatch (params string[] texts) => this.IsMatch((IReadOnlyList<string>)texts);

        /// <summary>
        /// Builds a new expression with the same structure and every term replaced by what <paramref name="map"/> gives for
        /// it. <paramref name="map"/> is called once for each entry in <see cref="Terms"/>, and terms that map to the same
        /// value become one term. Mapping one way and then back again renders the same text as the original.
        /// </summary>
        /// <exception cref="ArgumentException">When <paramref name="map"/> gives back a null or empty term</exception>
        public TermExpression MapTerms (Func<string, string> map)
        {
            ArgumentNullException.ThrowIfNull(map);

            // 1. Map each distinct term once, and fold together the ones that land on the same value so Terms still lists each
            //  term once. A term's folded index is where its mapped value first turned up.
            string[] mappedTerms = new string[m_terms.Length];
            int[] foldedIndexOf = new int[m_terms.Length];
            var foldedIndexByTerm = new Dictionary<string, int>(m_terms.Length, StringComparer.Ordinal);
            for (int termIndex = 0; termIndex < m_terms.Length; ++termIndex)
            {
                string mapped = map(m_terms[termIndex]);
                if (String.IsNullOrEmpty(mapped))
                {
                    throw new ArgumentException($"The map turned the term '{m_terms[termIndex]}' into an empty one", nameof(map));
                }

                if (!foldedIndexByTerm.TryGetValue(mapped, out int foldedIndex))
                {
                    foldedIndex = foldedIndexByTerm.Count;
                    foldedIndexByTerm.Add(mapped, foldedIndex);
                    mappedTerms[foldedIndex] = mapped;
                }

                foldedIndexOf[termIndex] = foldedIndex;
            }

            // 2. Nothing folded, which is the usual case: every node's term index is still right, so the nodes are shared
            //  with this expression. They never change, so sharing them is safe.
            if (foldedIndexByTerm.Count == m_terms.Length)
            {
                return new TermExpression(m_nodes, mappedTerms);
            }

            // 3. Something folded, so the term nodes get pointed at the folded terms
            Node[] nodes = new Node[m_nodes.Length];
            for (int nodeIndex = 0; nodeIndex < m_nodes.Length; ++nodeIndex)
            {
                Node node = m_nodes[nodeIndex];
                nodes[nodeIndex] = node.Kind == eNodeKind.Term
                    ? Node.ForTerm(foldedIndexOf[node.TermIndex], node.IsNegated)
                    : node;
            }

            return new TermExpression(nodes, mappedTerms[..foldedIndexByTerm.Count]);
        }

        /// <summary>
        /// The expression in its canonical form: single spaces around <c>&amp;</c> and <c>|</c>, parentheses only where
        /// <c>&amp;</c> and <c>|</c> meet or under a <c>!</c>, and quotes only around terms that need them. Parsing it gives
        /// back the same expression, whichever <see cref="eTermWhitespace"/> it's parsed with.
        /// </summary>
        public override string ToString ()
        {
            // An expression never changes, so it only needs rendering once. Two threads that race here both render the same
            //  text, so whichever one lands is right.
            if (m_canonicalText == null)
            {
                var output = new StringBuilder();
                this.RenderNode(0, output);
                m_canonicalText = output.ToString();
            }

            return m_canonicalText;
        }

        // ===========[ Helpers ]==============================================

        private bool EvaluateWith<TPresence> (ref TPresence presence)
            where TPresence : struct, ITermPresence
        {
            // 1. When every term shows up once, which is most expressions, nothing gets asked twice and there's nothing to
            //  remember
            if (!m_hasRepeatedTerms)
            {
                return this.EvaluateNode(0, Span<byte>.Empty, ref presence);
            }

            // 2. Otherwise the evaluation remembers what it learns about each distinct term, so a term that shows up more than
            //  once is only asked about once. The usual handful of terms fits on the stack, which starts out zeroed.
            const int kMostAnswersOnTheStack = 256;
            if (m_terms.Length > kMostAnswersOnTheStack)
            {
                return this.EvaluateWithRentedAnswers(ref presence);
            }

            Span<byte> answers = stackalloc byte[m_terms.Length];
            return this.EvaluateNode(0, answers, ref presence);
        }

        // Kept out of EvaluateWith so the try/finally the rented buffer needs is never on the usual path
        private bool EvaluateWithRentedAnswers<TPresence> (ref TPresence presence)
            where TPresence : struct, ITermPresence
        {
            byte[] rented = ArrayPool<byte>.Shared.Rent(m_terms.Length);
            try
            {
                // A rented buffer holds whatever its last renter left in it
                Span<byte> answers = rented.AsSpan(0, m_terms.Length);
                answers.Clear();
                return this.EvaluateNode(0, answers, ref presence);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        // answers is empty when the expression has no repeated terms, and holds an answer per distinct term when it does
        private bool EvaluateNode<TPresence> (int nodeIndex, Span<byte> answers, ref TPresence presence)
            where TPresence : struct, ITermPresence
        {
            ref readonly Node node = ref m_nodes[nodeIndex];
            if (node.Kind == eNodeKind.Term)
            {
                return this.EvaluateTerm(in node, answers, ref presence);
            }

            // An & group is true until a child comes out false, and an | group is false until a child comes out true. So the
            //  first child that disagrees with the group's starting value decides it, and the children after it are never
            //  evaluated (and their terms never asked about).
            //
            // Most children are terms, so a term child is evaluated right here instead of through another call, and only a
            //  group child recurses
            bool isAnd = node.Kind == eNodeKind.And;
            bool result = isAnd;
            int childIndex = nodeIndex + 1;
            for (int childNumber = 0; childNumber < node.ChildCount; ++childNumber)
            {
                ref readonly Node child = ref m_nodes[childIndex];
                bool childResult = child.Kind == eNodeKind.Term
                    ? this.EvaluateTerm(in child, answers, ref presence)
                    : this.EvaluateNode(childIndex, answers, ref presence);

                if (childResult != isAnd)
                {
                    result = !isAnd;
                    break;
                }

                childIndex += child.Size;
            }

            return result != node.IsNegated;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool EvaluateTerm<TPresence> (in Node term, Span<byte> answers, ref TPresence presence)
            where TPresence : struct, ITermPresence
        {
            bool isPresent;
            if (answers.IsEmpty)
            {
                isPresent = presence.IsPresent(m_terms[term.TermIndex]);
            }
            else
            {
                byte answer = answers[term.TermIndex];
                if (answer == kNotAskedYet)
                {
                    answer = presence.IsPresent(m_terms[term.TermIndex]) ? kPresent : kAbsent;
                    answers[term.TermIndex] = answer;
                }

                isPresent = answer == kPresent;
            }

            return isPresent != term.IsNegated;
        }

        private void RenderNode (int nodeIndex, StringBuilder output)
        {
            Node node = m_nodes[nodeIndex];
            if (node.IsNegated)
            {
                output.Append('!');
            }

            if (node.Kind == eNodeKind.Term)
            {
                AppendTerm(output, m_terms[node.TermIndex]);
                return;
            }

            // Parsing splices a group into its parent when they're the same kind, so every group below the top is a place
            //  where & and | meet, and needs its parentheses. So does any group under a '!'.
            bool needsParentheses = (nodeIndex != 0) || node.IsNegated;
            if (needsParentheses)
            {
                output.Append('(');
            }

            string separator = node.Kind == eNodeKind.And ? " & " : " | ";
            int childIndex = nodeIndex + 1;
            for (int childNumber = 0; childNumber < node.ChildCount; ++childNumber)
            {
                if (childNumber > 0)
                {
                    output.Append(separator);
                }

                this.RenderNode(childIndex, output);
                childIndex += m_nodes[childIndex].Size;
            }

            if (needsParentheses)
            {
                output.Append(')');
            }
        }

        // ===========[ Subclasses/structs ]===================================

        /// <summary>
        /// What a <see cref="Node"/> is.
        /// </summary>
        private enum eNodeKind : byte
        {
            /// <summary>
            /// A single term, present or not
            /// </summary>
            Term,

            /// <summary>
            /// A group that's true when all of its children are (<c>&amp;</c>)
            /// </summary>
            And,

            /// <summary>
            /// A group that's true when any of its children is (<c>|</c>)
            /// </summary>
            Or,
        }

        /// <summary>
        /// One step of the expression. The steps are kept in one flat array in depth first order: a group's first child comes
        /// right after it, and each child after that comes right after the subtree of the one before it.
        /// </summary>
        /// <remarks>
        /// <see cref="Size"/> is how many entries a subtree covers, which is what lets evaluation step from one child to the
        /// next, and skip the rest of a group once it's decided, with no references between nodes at all. The flat layout is
        /// for speed: the whole expression is one allocation that gets walked in order, and evaluation is one method made
        /// generic over the presence test (so it gets compiled once per test with the test's call inlined) instead of a
        /// virtual call on every node.
        /// </remarks>
        private readonly struct Node
        {
            private Node (eNodeKind kind, bool isNegated, int termIndex, int childCount, int size)
            {
                this.Kind = kind;
                this.IsNegated = isNegated;
                this.TermIndex = termIndex;
                this.ChildCount = childCount;
                this.Size = size;
            }

            public static Node ForTerm (int termIndex, bool isNegated)
            {
                return new Node(eNodeKind.Term, isNegated, termIndex, childCount: 0, size: 1);
            }

            public static Node ForGroup (eNodeKind kind, bool isNegated, int childCount, int size)
            {
                return new Node(kind, isNegated, termIndex: -1, childCount, size);
            }

            public eNodeKind Kind { get; }

            /// <summary>
            /// Whether a <c>!</c> flips this step's result
            /// </summary>
            public bool IsNegated { get; }

            /// <summary>
            /// Which term a <see cref="eNodeKind.Term"/> step tests, as an index into the expression's terms. -1 for a group.
            /// </summary>
            public int TermIndex { get; }

            /// <summary>
            /// How many children a group has. 0 for a term.
            /// </summary>
            public int ChildCount { get; }

            /// <summary>
            /// How many entries this step and everything under it cover in the array
            /// </summary>
            public int Size { get; }
        }

        /// <summary>
        /// What <see cref="Parse"/> gives back: the expression, or what's wrong with the text and where.
        /// </summary>
        public sealed class ParseResult : Result<TermExpression>
        {
            internal ParseResult (TermExpression expression) : base(expression)
            {
                this.ErrorIndex = -1;
            }

            internal ParseResult (string error, int errorIndex)
            {
                this.AddError(error);
                this.ErrorIndex = errorIndex;
            }

            /// <summary>
            /// Where in the parsed text the problem is, as an index (from 0) you can point at. -1 when the parse succeeded.
            /// The error itself gives the same spot as a position counted from 1, for whoever's reading it.
            /// </summary>
            public int ErrorIndex { get; }
        }
    }
}
