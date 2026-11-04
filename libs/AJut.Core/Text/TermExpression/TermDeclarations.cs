namespace AJut.Text
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The values a host supplies to its formulas, each by name and kind, and whether it can be left unset. Parsing checks a
    /// formula against these while the author types, and <see cref="CreateValues"/> makes the <see cref="TermValues"/> to fill
    /// in for each evaluation.
    /// </summary>
    /// <remarks>
    /// <para>Names are what follows the '$' in a formula: letters, digits, and '_', and a name may start with a digit.</para>
    /// <para>A declaration can carry a print rule, the host's own way of printing that value wherever a formula prints it,
    /// so the host doesn't have to label every value. A label on the value itself still wins, and anything computed from it
    /// prints the default way.</para>
    /// <para>Stops changing the first time it's used to parse or to create values, so everything parsed against it stays
    /// right. Declaring anything after that throws.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// TermDeclarations attack = new TermDeclarations(functions)
    ///     .IntList("d")
    ///     .Int("modifier", optional: true)
    ///     .Float("chance", print: value => (value * 100).ToString("0", CultureInfo.InvariantCulture) + "%");
    /// </code>
    /// </example>
    public sealed class TermDeclarations
    {
        private readonly List<Entry> m_entries = new List<Entry>();
        private readonly List<string> m_names = new List<string>();
        private readonly Dictionary<string, Entry> m_entryByName = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly object m_freezeLock = new object();
        private Dictionary<string, Entry> m_entryByNameIgnoringCase;
        private string m_caseClash;
        private volatile bool m_isFrozen;

        // ===========[ Setup/Construction/Teardown ]==========================

        /// <summary>
        /// Starts an empty set of declarations, with the table and host functions formulas parsed against it can call.
        /// </summary>
        public TermDeclarations (TermFunctions functions = null)
        {
            this.Functions = functions;
            this.Names = m_names.AsReadOnly();
        }

        // ===========[ Properties ]===========================================

        /// <summary>
        /// The table and host functions formulas parsed against these can call, or null for the built-ins only.
        /// </summary>
        public TermFunctions Functions { get; }

        /// <summary>
        /// Every declared name, in the order declared, for an editor to offer.
        /// </summary>
        public IReadOnlyList<string> Names { get; }

        // ===========[ Public Interface Methods ]=============================

        /// <summary>
        /// Declares a whole number.
        /// </summary>
        /// <param name="name">What follows the '$' in a formula</param>
        /// <param name="optional">Whether it can be left unset, in which case a formula has to check it with has() first</param>
        /// <param name="print">How to print it, when it has no label. Host code: not sandboxed, and not caught.</param>
        public TermDeclarations Int (string name, bool optional = false, Func<long, string> print = null)
        {
            // Read as the number rather than AsInt, since a whole number taken where a number is wanted keeps its print rule
            return this.Add(name, eTermKind.Int, optional, print == null ? null : value => print((long)value.Number));
        }

        /// <summary>
        /// Declares a number that can have a fraction. A whole number can be set for it too.
        /// </summary>
        /// <inheritdoc cref="Int(string, bool, Func{long, string})"/>
        public TermDeclarations Float (string name, bool optional = false, Func<double, string> print = null)
        {
            return this.Add(name, eTermKind.Float, optional, print == null ? null : value => print(value.AsFloat));
        }

        /// <summary>
        /// Declares true or false.
        /// </summary>
        /// <inheritdoc cref="Int(string, bool, Func{long, string})"/>
        public TermDeclarations Bool (string name, bool optional = false, Func<bool, string> print = null)
        {
            return this.Add(name, eTermKind.Bool, optional, print == null ? null : value => print(value.AsBool));
        }

        /// <summary>
        /// Declares text.
        /// </summary>
        /// <inheritdoc cref="Int(string, bool, Func{long, string})"/>
        public TermDeclarations String (string name, bool optional = false, Func<string, string> print = null)
        {
            return this.Add(name, eTermKind.String, optional, print == null ? null : value => print(value.AsString));
        }

        /// <summary>
        /// Declares a list of whole numbers, like a set of scores. The print rule is for each item.
        /// </summary>
        /// <inheritdoc cref="Int(string, bool, Func{long, string})"/>
        public TermDeclarations IntList (string name, bool optional = false, Func<long, string> print = null)
        {
            return this.Add(name, eTermKind.IntList, optional, print == null ? null : value => print((long)value.Number));
        }

        /// <summary>
        /// Declares a list of numbers that can have fractions. The print rule is for each item.
        /// </summary>
        /// <inheritdoc cref="Int(string, bool, Func{long, string})"/>
        public TermDeclarations FloatList (string name, bool optional = false, Func<double, string> print = null)
        {
            return this.Add(name, eTermKind.FloatList, optional, print == null ? null : value => print(value.AsFloat));
        }

        /// <summary>
        /// Declares a list of true or false values. The print rule is for each item.
        /// </summary>
        /// <inheritdoc cref="Int(string, bool, Func{long, string})"/>
        public TermDeclarations BoolList (string name, bool optional = false, Func<bool, string> print = null)
        {
            return this.Add(name, eTermKind.BoolList, optional, print == null ? null : value => print(value.AsBool));
        }

        /// <summary>
        /// Declares a list of texts. The print rule is for each item.
        /// </summary>
        /// <inheritdoc cref="Int(string, bool, Func{long, string})"/>
        public TermDeclarations StringList (string name, bool optional = false, Func<string, string> print = null)
        {
            return this.Add(name, eTermKind.StringList, optional, print == null ? null : value => print(value.AsString));
        }

        /// <summary>
        /// The kind <paramref name="name"/> was declared as, when it was declared.
        /// </summary>
        public bool TryGetKind (string name, out eTermKind kind)
        {
            bool found = m_entryByName.TryGetValue(name ?? string.Empty, out Entry entry);
            kind = found ? entry.Kind : default;
            return found;
        }

        /// <summary>
        /// Whether <paramref name="name"/> was declared optional.
        /// </summary>
        public bool IsOptional (string name)
            => m_entryByName.TryGetValue(name ?? string.Empty, out Entry entry) && entry.IsOptional;

        /// <summary>
        /// Makes an empty set of values to fill in for one evaluation. These declarations stop changing from here on.
        /// </summary>
        public TermValues CreateValues ()
        {
            this.Freeze();
            return new TermValues(this);
        }

        // ===========[ Internal Interface Methods ]===========================

        internal int Count => m_entries.Count;

        internal Entry this[int index] => m_entries[index];

        internal bool TryFind (string name, bool ignoreCase, out Entry entry)
        {
            if (!ignoreCase)
            {
                return m_entryByName.TryGetValue(name, out entry);
            }

            this.Freeze();
            return m_entryByNameIgnoringCase.TryGetValue(name, out entry);
        }

        /// <summary>
        /// Stops these changing, ready to parse against.
        /// </summary>
        /// <exception cref="ArgumentException">When <paramref name="ignoreCase"/> is asked for and two declared names differ
        /// only in case</exception>
        internal void Freeze (bool ignoreCase = false)
        {
            if (!m_isFrozen)
            {
                lock (m_freezeLock)
                {
                    if (!m_isFrozen)
                    {
                        m_entryByNameIgnoringCase = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
                        foreach (Entry entry in m_entries)
                        {
                            if (!m_entryByNameIgnoringCase.TryAdd(entry.Name, entry))
                            {
                                m_caseClash ??= $"'{m_entryByNameIgnoringCase[entry.Name].Name}' and '{entry.Name}'";
                            }
                        }

                        m_isFrozen = true;
                    }
                }
            }

            if (ignoreCase && (m_caseClash != null))
            {
                throw new ArgumentException($"With names ignoring case, the declared names {m_caseClash} are the same name");
            }
        }

        /// <summary>
        /// For DeclarationsWithResult, which builds a new set from a formula's and keeps each entry's print rule
        /// </summary>
        internal TermDeclarations AddEntry (string name, eTermKind kind, bool optional, Func<TermValue, string> print)
        {
            return this.Add(name, kind, optional, print);
        }

        // ===========[ Helpers ]==============================================

        private TermDeclarations Add (string name, eTermKind kind, bool optional, Func<TermValue, string> print)
        {
            if (m_isFrozen)
            {
                throw new InvalidOperationException(
                    "These declarations have been used to parse or to create values, so they can't change"
                );
            }

            if (!FormulaLexer.IsValidName(name, isFunction: false))
            {
                throw new ArgumentException(
                    $"'{name}' isn't a name a formula can use: use letters, digits, and '_'",
                    nameof(name)
                );
            }

            if (m_entryByName.ContainsKey(name))
            {
                throw new ArgumentException($"'{name}' is already declared", nameof(name));
            }

            var entry = new Entry(name, kind, optional, print, m_entries.Count);
            m_entries.Add(entry);
            m_names.Add(name);
            m_entryByName.Add(name, entry);
            return this;
        }

        // ===========[ Subclasses/structs ]===================================

        /// <summary>
        /// One declared value.
        /// </summary>
        internal sealed class Entry
        {
            public Entry (string name, eTermKind kind, bool isOptional, Func<TermValue, string> print, int index)
            {
                this.Name = name;
                this.Kind = kind;
                this.IsOptional = isOptional;
                this.Print = print;
                this.Index = index;
            }

            public string Name { get; }
            public eTermKind Kind { get; }
            public bool IsOptional { get; }

            /// <summary>
            /// The host's print rule, for a single value or each item of a list, or null
            /// </summary>
            public Func<TermValue, string> Print { get; }

            /// <summary>
            /// Where it is in its declarations, which is where its value is in a <see cref="TermValues"/> made from them
            /// </summary>
            public int Index { get; }
        }
    }
}
