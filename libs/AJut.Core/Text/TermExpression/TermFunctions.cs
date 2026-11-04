namespace AJut.Text
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// A table's functions, the ones its authors wrote as text and the ones its host wrote in C#, parsed once with
    /// <see cref="TermExpression.ParseFunctions"/> and handed to <see cref="TermDeclarations"/> so formulas can call them.
    /// </summary>
    /// <remarks>
    /// <para>A table function's body is checked for names and calls when the set is parsed, so a typo shows up as soon as
    /// it's entered, and functions that call each other in a circle are refused then too. Kinds come from each call, so the
    /// body is checked for kinds once for each set of argument kinds it's called with, and remembered.</para>
    /// <para>One broken function doesn't take the rest of the table down with it: the others still work, and a call to the
    /// broken one fails at the call.</para>
    /// <para>Safe to share across threads once parsed.</para>
    /// </remarks>
    public sealed class TermFunctions
    {
        private readonly Entry[] m_entries;
        private readonly Dictionary<string, Entry> m_entryByName;
        private readonly Dictionary<string, TermHostFunction> m_hostByName;
        private readonly Dictionary<string, Specialization> m_specializations
            = new Dictionary<string, Specialization>(StringComparer.Ordinal);
        private readonly object m_specializationsLock = new object();

        // ===========[ Setup/Construction/Teardown ]==========================

        private TermFunctions (
            Entry[] entries,
            Dictionary<string, Entry> entryByName,
            Dictionary<string, TermHostFunction> hostByName,
            bool ignoresNameCase
        )
        {
            m_entries = entries;
            m_entryByName = entryByName;
            m_hostByName = hostByName;
            this.IgnoresNameCase = ignoresNameCase;

            var listed = new List<TermFunctionInfo>();
            foreach (Entry entry in entries)
            {
                if (entryByName.TryGetValue(entry.Name, out Entry registered) && ReferenceEquals(registered, entry))
                {
                    string usage = $"{entry.Name}({String.Join(", ", entry.Parameters)})";
                    listed.Add(new TermFunctionInfo(entry.Name, usage, null, eTermFunctionSource.Table, entry.Parameters));
                }
            }

            foreach (TermHostFunction host in hostByName.Values)
            {
                var kinds = new List<string>();
                foreach (eTermKind kind in host.ParameterKinds)
                {
                    kinds.Add(TermKinds.Describe(kind));
                }

                listed.Add(new TermFunctionInfo(
                    host.Name,
                    $"{host.Name}({String.Join(", ", kinds)})",
                    null,
                    eTermFunctionSource.Host,
                    null
                ));
            }

            this.Functions = listed.AsReadOnly();
        }

        // ===========[ Properties ]===========================================

        /// <summary>
        /// Every function in the set, table functions first, for an editor to list or complete. The built-ins are in
        /// <see cref="TermExpression.BuiltInFunctions"/>.
        /// </summary>
        public IReadOnlyList<TermFunctionInfo> Functions { get; }

        internal bool IgnoresNameCase { get; }

        internal IEnumerable<string> Names
        {
            get
            {
                foreach (string name in m_entryByName.Keys)
                {
                    yield return name;
                }

                foreach (string name in m_hostByName.Keys)
                {
                    yield return name;
                }
            }
        }

        // ===========[ Internal Interface Methods ]===========================

        internal static TermParseResult<TermFunctions> Parse (
            IEnumerable<TermFunctionSource> sources,
            IEnumerable<TermHostFunction> hostFunctions,
            TermParseOptions options
        )
        {
            ArgumentNullException.ThrowIfNull(sources);
            TermParseOptions.ThrowIfOutOfRange(options);
            StringComparer comparer = options.IgnoreNameCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            int maxDepth = options.MaxDepthOrDefault;
            StringComparison keywordComparison = options.IgnoreNameCase
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            // 1. Host functions are the host's own code, so a mistake in them throws
            var hostByName = new Dictionary<string, TermHostFunction>(comparer);
            foreach (TermHostFunction host in hostFunctions ?? Array.Empty<TermHostFunction>())
            {
                ArgumentNullException.ThrowIfNull(host, nameof(hostFunctions));
                if (!hostByName.TryAdd(host.Name, host))
                {
                    throw new ArgumentException($"Two host functions are named {host.Name}", nameof(hostFunctions));
                }
            }

            // 2. Table functions are what an author typed, so a mistake in one is an error on that entry
            var entries = new List<Entry>();
            var entryByName = new Dictionary<string, Entry>(comparer);
            foreach (TermFunctionSource source in sources)
            {
                ArgumentNullException.ThrowIfNull(source, nameof(sources));
                var entry = new Entry(source.Name, ToArray(source.Parameters), source.Body, entries.Count);
                entries.Add(entry);

                if (!FormulaLexer.IsValidName(entry.Name, isFunction: true))
                {
                    entry.Error = new TermError(
                        $"'{entry.Name}' isn't a function name: start with a letter or '_', then use letters, digits, and '_'.",
                        -1,
                        entry.Name
                    );
                    continue;
                }

                if (String.Equals(entry.Name, "true", keywordComparison) || String.Equals(entry.Name, "false", keywordComparison))
                {
                    entry.Error = new TermError(
                        $"'{entry.Name}' can't be a function's name, since it's a keyword.",
                        -1,
                        entry.Name
                    );
                    continue;
                }

                if (!entryByName.TryAdd(entry.Name, entry))
                {
                    entry.Error = new TermError($"Another function is already named {entry.Name}.", -1, entry.Name);
                    continue;
                }

                entry.Error = CheckParameters(entry, comparer);
                if (entry.Error != null)
                {
                    continue;
                }

                entry.Statements = FormulaParser.ParseFormula(
                    entry.Body,
                    maxDepth,
                    options.IgnoreNameCase,
                    out TermError syntaxError
                );
                if (syntaxError != null)
                {
                    entry.Error = new TermError(syntaxError.Message, syntaxError.Index, entry.Name);
                }
            }

            var functions = new TermFunctions(entries.ToArray(), entryByName, hostByName, options.IgnoreNameCase);

            // 3. Names and calls in each body, which needs every entry known first, then cycles, which needs every call known
            foreach (Entry entry in entries)
            {
                if (entry.Error == null)
                {
                    entry.Error = new BodyResolver(functions, entry, comparer).Run();
                }
            }

            functions.RefuseCycles();

            var errors = new List<TermError>();
            foreach (Entry entry in entries)
            {
                if (entry.Error != null)
                {
                    errors.Add(entry.Error);
                }
            }

            return errors.Count == 0
                ? new TermParseResult<TermFunctions>(functions)
                : new TermParseResult<TermFunctions>(errors, functions);
        }

        internal bool TryFindTable (string name, bool ignoreCase, out Entry entry)
        {
            if (m_entryByName.TryGetValue(name, out entry))
            {
                return true;
            }

            // A set parsed matching case can still be called by a formula that ignores it
            if (ignoreCase && !this.IgnoresNameCase)
            {
                foreach (Entry candidate in m_entryByName.Values)
                {
                    if (String.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        internal bool TryFindHost (string name, bool ignoreCase, out TermHostFunction host)
        {
            if (m_hostByName.TryGetValue(name, out host))
            {
                return true;
            }

            if (ignoreCase && !this.IgnoresNameCase)
            {
                foreach (TermHostFunction candidate in m_hostByName.Values)
                {
                    if (String.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        host = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// The body of <paramref name="entry"/> checked for <paramref name="argumentKinds"/>, from a call that puts the body at
        /// <paramref name="baseDepth"/>, or null with the reason when it doesn't check.
        /// </summary>
        internal FormulaFunctionBody Specialize (
            Entry entry,
            eTermKind[] argumentKinds,
            int baseDepth,
            int maxDepth,
            out CheckedFormula failure
        )
        {
            failure = null;
            if (baseDepth > maxDepth)
            {
                failure = DepthFailure(maxDepth);
                return null;
            }

            string key = KeyFor(entry, argumentKinds);

            // Checking a body can specialize the functions it calls, which takes this lock again on the same thread, and a
            //  Monitor allows that
            lock (m_specializationsLock)
            {
                if (m_specializations.TryGetValue(key, out Specialization known))
                {
                    if (known.Failure != null)
                    {
                        failure = known.Failure;
                        return null;
                    }

                    if (baseDepth + known.Body.Height > maxDepth)
                    {
                        failure = DepthFailure(maxDepth);
                        return null;
                    }

                    return known.Body;
                }

                CheckedFormula checkedBody = FormulaChecker.CheckFunctionBody(this, entry, argumentKinds, baseDepth, maxDepth);
                if (!checkedBody.IsSuccess)
                {
                    // Failing only on depth says nothing about a call from somewhere shallower, so that isn't remembered
                    if (!checkedBody.FailedOnDepth)
                    {
                        m_specializations.Add(key, new Specialization(null, checkedBody));
                    }

                    failure = checkedBody;
                    return null;
                }

                var body = new FormulaFunctionBody(
                    entry.Name,
                    checkedBody.Program,
                    checkedBody.ResultKind,
                    checkedBody.Cost,
                    checkedBody.Height
                );
                m_specializations.Add(key, new Specialization(body, null));
                return body;
            }
        }

        // ===========[ Helpers ]==============================================

        private static string[] ToArray (IReadOnlyList<string> parameters)
        {
            var copied = new string[parameters.Count];
            for (int index = 0; index < copied.Length; ++index)
            {
                copied[index] = parameters[index];
            }

            return copied;
        }

        private static TermError CheckParameters (Entry entry, StringComparer comparer)
        {
            var seen = new HashSet<string>(comparer);
            foreach (string parameter in entry.Parameters)
            {
                if (!FormulaLexer.IsValidName(parameter, isFunction: false))
                {
                    return new TermError(
                        $"'{parameter}' isn't a parameter name: use letters, digits, and '_', with no '$'.",
                        -1,
                        entry.Name
                    );
                }

                if (!seen.Add(parameter))
                {
                    return new TermError($"{entry.Name} has two parameters named {parameter}.", -1, entry.Name);
                }
            }

            return null;
        }

        private static string KeyFor (Entry entry, eTermKind[] argumentKinds)
        {
            var key = new StringBuilder(argumentKinds.Length + 8);
            key.Append(entry.Index).Append(':');
            foreach (eTermKind kind in argumentKinds)
            {
                key.Append((char)('a' + (int)kind));
            }

            return key.ToString();
        }

        private static CheckedFormula DepthFailure (int maxDepth)
        {
            string message = $"Nesting and functions calling functions go more than {maxDepth} deep.";
            return CheckedFormula.Failed(
                new TermError(message, 0),
                null,
                FormulaMessages.LowerFirst(message),
                failedOnDepth: true
            );
        }

        // A function that can reach itself through its calls is on a cycle. Each one gets an error at its first call
        //  into the cycle, saying the way around.
        private void RefuseCycles ()
        {
            foreach (Entry entry in m_entries)
            {
                if ((entry.Error != null) || (entry.Calls.Count == 0))
                {
                    continue;
                }

                // Breadth first from each of its calls, remembering how each function was reached
                var reachedFrom = new Dictionary<Entry, Entry>();
                var queue = new Queue<Entry>();
                foreach (Call call in entry.Calls)
                {
                    if (!reachedFrom.ContainsKey(call.Callee))
                    {
                        reachedFrom.Add(call.Callee, entry);
                        queue.Enqueue(call.Callee);
                    }
                }

                bool isOnCycle = false;
                while ((queue.Count > 0) && !isOnCycle)
                {
                    Entry next = queue.Dequeue();
                    if (ReferenceEquals(next, entry))
                    {
                        isOnCycle = true;
                        break;
                    }

                    foreach (Call call in next.Calls)
                    {
                        if (!reachedFrom.ContainsKey(call.Callee))
                        {
                            reachedFrom.Add(call.Callee, next);
                            queue.Enqueue(call.Callee);
                        }
                    }
                }

                if (isOnCycle)
                {
                    entry.CycleError = this.DescribeCycle(entry, reachedFrom);
                }
            }

            // Set after the search, so every function on a cycle is found, not only the first
            foreach (Entry entry in m_entries)
            {
                if (entry.CycleError != null)
                {
                    entry.Error = entry.CycleError;
                }
            }
        }

        private TermError DescribeCycle (Entry entry, Dictionary<Entry, Entry> reachedFrom)
        {
            // Walk back from the entry to find the way around: entry, first callee, ..., entry
            var path = new List<Entry> { entry };
            Entry step = reachedFrom[entry];
            while (!ReferenceEquals(step, entry))
            {
                path.Add(step);
                step = reachedFrom[step];
            }

            path.Add(entry);
            path.Reverse();

            Entry firstCallee = path[1];
            int callIndex = 0;
            foreach (Call call in entry.Calls)
            {
                if (ReferenceEquals(call.Callee, firstCallee))
                {
                    callIndex = call.Index;
                    break;
                }
            }

            if (ReferenceEquals(firstCallee, entry))
            {
                return new TermError(
                    $"{entry.Name} calls itself at {FormulaMessages.At(callIndex)}, and a function can't call itself.",
                    callIndex,
                    entry.Name
                );
            }

            var story = new StringBuilder($"{entry.Name} calls {firstCallee.Name} at {FormulaMessages.At(callIndex)}");
            for (int index = 2; index < path.Count; ++index)
            {
                story.Append($", which calls {path[index].Name}");
            }

            story.Append(", and functions can't call each other in a circle.");
            return new TermError(story.ToString(), callIndex, entry.Name);
        }

        // ===========[ Subclasses/structs ]===================================

        /// <summary>
        /// One table function, as it was entered and as far as it parsed.
        /// </summary>
        internal sealed class Entry
        {
            public Entry (string name, string[] parameters, string body, int index)
            {
                this.Name = name;
                this.Parameters = parameters;
                this.Body = body;
                this.Index = index;
            }

            public string Name { get; }
            public string[] Parameters { get; }
            public string Body { get; }

            /// <summary>
            /// Where it was in the sources, which tells it apart from another with the same name
            /// </summary>
            public int Index { get; }

            public List<FormulaSyntax> Statements { get; set; }

            /// <summary>
            /// What's wrong with it, or null when it can be called
            /// </summary>
            public TermError Error { get; set; }

            public TermError CycleError { get; set; }

            /// <summary>
            /// Each call its body makes to another table function, in the order written
            /// </summary>
            public List<Call> Calls { get; } = new List<Call>();
        }

        internal readonly record struct Call (Entry Callee, int Index);

        private sealed record Specialization (FormulaFunctionBody Body, CheckedFormula Failure);

        /// <summary>
        /// Checks the names in one body and finds its calls, before any kinds are known.
        /// </summary>
        private sealed class BodyResolver
        {
            private readonly TermFunctions m_functions;
            private readonly Entry m_entry;
            private readonly HashSet<string> m_parameters;
            private readonly HashSet<string> m_steps;
            private readonly Dictionary<string, List<int>> m_stepPositions;
            private TermError m_error;

            public BodyResolver (TermFunctions functions, Entry entry, StringComparer comparer)
            {
                m_functions = functions;
                m_entry = entry;
                m_parameters = new HashSet<string>(entry.Parameters, comparer);
                m_steps = new HashSet<string>(comparer);
                m_stepPositions = new Dictionary<string, List<int>>(comparer);
            }

            public TermError Run ()
            {
                foreach (FormulaSyntax statement in m_entry.Statements)
                {
                    if (statement is FormulaSyntax.Step step)
                    {
                        if (!m_stepPositions.TryGetValue(step.Name, out List<int> positions))
                        {
                            positions = new List<int>();
                            m_stepPositions.Add(step.Name, positions);
                        }

                        positions.Add(step.Start);
                    }
                }

                foreach (FormulaSyntax statement in m_entry.Statements)
                {
                    if (statement is FormulaSyntax.Step step)
                    {
                        if (m_parameters.Contains(step.Name))
                        {
                            return this.Fail(
                                step.Start,
                                $"${step.Name} at {FormulaMessages.At(step.Start)} is one of {m_entry.Name}'s parameters,"
                                    + " so a step can't set it. Give the step its own name."
                            );
                        }

                        this.Walk(step.Value);
                        m_steps.Add(step.Name);
                    }
                    else
                    {
                        this.Walk(statement);
                    }

                    if (m_error != null)
                    {
                        return m_error;
                    }
                }

                return null;
            }

            private void Walk (FormulaSyntax syntax)
            {
                if (m_error != null)
                {
                    return;
                }

                switch (syntax)
                {
                    case FormulaSyntax.Variable variable:
                        this.CheckName(variable);
                        break;

                    case FormulaSyntax.Text text:
                        foreach (FormulaSyntax.TextPart part in text.Parts)
                        {
                            if (part.Expression != null)
                            {
                                this.Walk(part.Expression);
                            }
                        }

                        break;

                    case FormulaSyntax.Group group:
                        this.Walk(group.Inner);
                        break;

                    case FormulaSyntax.Unary unary:
                        this.Walk(unary.Operand);
                        break;

                    case FormulaSyntax.Chain chain:
                        {
                            foreach (FormulaSyntax operand in chain.Operands)
                            {
                                this.Walk(operand);
                            }

                            string zeroDivisor = FormulaBuiltIns.WrittenZeroDivisorProblem(chain, out int zeroAt);
                            if (zeroDivisor != null)
                            {
                                this.Fail(zeroAt, zeroDivisor);
                            }

                            break;
                        }

                    case FormulaSyntax.Conditional conditional:
                        this.Walk(conditional.Condition);
                        this.Walk(conditional.WhenTrue);
                        this.Walk(conditional.WhenFalse);
                        break;

                    case FormulaSyntax.Call call:
                        this.CheckCall(call);
                        foreach (FormulaSyntax argument in call.Arguments)
                        {
                            this.Walk(argument);
                        }

                        break;
                }
            }

            private void CheckName (FormulaSyntax.Variable variable)
            {
                if (m_parameters.Contains(variable.Name) || m_steps.Contains(variable.Name))
                {
                    return;
                }

                if (m_stepPositions.TryGetValue(variable.Name, out List<int> positions))
                {
                    foreach (int setAt in positions)
                    {
                        if (setAt > variable.Start)
                        {
                            this.Fail(
                                variable.Start,
                                $"${variable.Name} is used before it's set ({FormulaMessages.At(variable.Start)};"
                                    + $" it's set at {FormulaMessages.At(setAt)})."
                            );
                            return;
                        }
                    }
                }

                // Set further up, but not yet: this is the value it's first being set to
                if (positions != null)
                {
                    this.Fail(
                        variable.Start,
                        $"${variable.Name} at {FormulaMessages.At(variable.Start)} is used in its own first definition,"
                            + " before it has a value."
                    );
                    return;
                }

                var known = new List<string>(m_parameters);
                known.AddRange(m_steps);
                string parameters = m_entry.Parameters.Length == 0
                    ? $"{m_entry.Name} has none"
                    : $"{m_entry.Name}'s are ${String.Join(", $", m_entry.Parameters)}";

                this.Fail(
                    variable.Start,
                    $"Unknown name '${variable.Name}' at {FormulaMessages.At(variable.Start)}."
                        + $"{FormulaMessages.Suggest(variable.Name, known, "$")} A function only sees its parameters,"
                        + $" and {parameters}."
                );
            }

            private void CheckCall (FormulaSyntax.Call call)
            {
                bool ignoreCase = m_functions.IgnoresNameCase;
                if (m_functions.TryFindTable(call.Name, ignoreCase, out Entry callee))
                {
                    if (call.Arguments.Count != callee.Parameters.Length)
                    {
                        this.Fail(
                            call.Start,
                            $"{callee.Name}() takes {callee.Parameters.Length}"
                                + $" argument{(callee.Parameters.Length == 1 ? "" : "s")}, and is given {call.Arguments.Count},"
                                + $" at {FormulaMessages.At(call.Start)}."
                        );
                        return;
                    }

                    m_entry.Calls.Add(new Call(callee, call.Start));
                    return;
                }

                if (m_functions.TryFindHost(call.Name, ignoreCase, out TermHostFunction host))
                {
                    if (call.Arguments.Count != host.ParameterKinds.Count)
                    {
                        this.Fail(
                            call.Start,
                            $"{host.Name}() takes {host.ParameterKinds.Count}"
                                + $" argument{(host.ParameterKinds.Count == 1 ? "" : "s")}, and is given {call.Arguments.Count},"
                                + $" at {FormulaMessages.At(call.Start)}."
                        );
                    }

                    return;
                }

                if (FormulaBuiltIns.TryFind(call.Name, ignoreCase, out FormulaBuiltIns.eBuiltIn builtIn))
                {
                    string wrongCount = FormulaChecker.WrongArgumentCount(builtIn, call.Arguments.Count, call.Start);
                    if (wrongCount != null)
                    {
                        this.Fail(call.Start, wrongCount);
                        return;
                    }

                    string written = FormulaBuiltIns.WrittenArgumentProblem(
                        builtIn,
                        FormulaChecker.CanonicalName(builtIn),
                        call.Arguments,
                        call.Start,
                        out int writtenAt
                    );

                    if (written != null)
                    {
                        this.Fail(writtenAt, written);
                    }
                }
                else
                {
                    var known = new List<string>(m_functions.Names);
                    known.AddRange(FormulaBuiltIns.Names);
                    this.Fail(
                        call.Start,
                        $"Unknown function '{call.Name}' at {FormulaMessages.At(call.Start)}."
                            + $"{FormulaMessages.Suggest(call.Name, known, String.Empty)}"
                    );
                }
            }

            private TermError Fail (int index, string message)
            {
                m_error ??= new TermError(message, index, m_entry.Name);
                return m_error;
            }
        }
    }
}
