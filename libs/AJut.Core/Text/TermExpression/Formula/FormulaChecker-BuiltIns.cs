namespace AJut.Text
{
    using eBuiltIn = FormulaBuiltIns.eBuiltIn;

    /// <summary>
    /// What each built-in takes and gives back
    /// </summary>
    internal sealed partial class FormulaChecker
    {
        // ===========[ Helpers ]==============================================

        private FormulaNode CheckBuiltIn (FormulaSyntax.Call call, eBuiltIn builtIn)
        {
            string wrongCount = WrongArgumentCount(builtIn, call.Arguments.Count, call.Start);
            if (wrongCount != null)
            {
                return this.Fail(call.Start, wrongCount);
            }

            string written = FormulaBuiltIns.WrittenArgumentProblem(
                builtIn,
                CanonicalName(builtIn),
                call.Arguments,
                call.Start,
                out int writtenAt
            );

            if (written != null)
            {
                return this.Fail(writtenAt, written);
            }

            if (builtIn == eBuiltIn.Has)
            {
                return this.CheckHas(call);
            }

            string name = CanonicalName(builtIn);
            FormulaNode[] arguments = this.CheckArguments(call);
            if (arguments == null)
            {
                return null;
            }

            eTermKind first = arguments[0].Kind;
            bool isList = TermKinds.IsList(first);
            switch (builtIn)
            {
                case eBuiltIn.Sum:
                case eBuiltIn.Min:
                case eBuiltIn.Max:
                case eBuiltIn.Avg:
                    return this.CheckNumbersOrList(call, builtIn, name, arguments);

                case eBuiltIn.Any:
                case eBuiltIn.All:
                    if (!isList)
                    {
                        return this.Fail(
                            call.Arguments[0].Start,
                            $"{name}() wants a list, and {DescribeSingleArgument(call.Arguments[0], arguments[0])}"
                                + $" here ({At(call.Arguments[0].Start)})."
                        );
                    }

                    if (first != eTermKind.BoolList)
                    {
                        return this.Fail(
                            call.Arguments[0].Start,
                            $"{name}() wants a list of true or false values, like {name}($scores > 3),"
                                + $" and {What(call.Arguments[0])} is {TermKinds.Describe(first)}"
                                + $" ({At(call.Arguments[0].Start)})."
                        );
                    }

                    return new FormulaNode.BuiltIn(call.Start, eTermKind.Bool, builtIn, name, arguments);

                case eBuiltIn.Count:
                    if (!isList)
                    {
                        return this.Fail(
                            call.Arguments[0].Start,
                            $"count() wants a list, and {DescribeSingleArgument(call.Arguments[0], arguments[0])}"
                                + $" here ({At(call.Arguments[0].Start)})."
                        );
                    }

                    return new FormulaNode.BuiltIn(call.Start, eTermKind.Int, builtIn, name, arguments);

                case eBuiltIn.Highest:
                case eBuiltIn.Lowest:
                    if (!isList || !TermKinds.IsNumber(first))
                    {
                        return this.Fail(
                            call.Arguments[0].Start,
                            $"{name}() wants a list of numbers first, and {What(call.Arguments[0])}"
                                + $" is {TermKinds.Describe(first)} ({At(call.Arguments[0].Start)})."
                        );
                    }

                    return this.CheckWholeNumber(call, arguments, 1, $"{name}() wants a whole number for how many to keep")
                        ? new FormulaNode.BuiltIn(call.Start, first, builtIn, name, arguments)
                        : null;

                case eBuiltIn.Join:
                    if (!isList)
                    {
                        return this.Fail(
                            call.Arguments[0].Start,
                            $"join() wants a list, and {DescribeSingleArgument(call.Arguments[0], arguments[0])}"
                                + $" here ({At(call.Arguments[0].Start)})."
                        );
                    }

                    if ((arguments.Length == 2) && (arguments[1].Kind != eTermKind.String))
                    {
                        return this.Fail(
                            call.Arguments[1].Start,
                            $"join() wants text to put between the items, and {What(call.Arguments[1])}"
                                + $" is {TermKinds.Describe(arguments[1].Kind)} ({At(call.Arguments[1].Start)})."
                        );
                    }

                    return new FormulaNode.BuiltIn(call.Start, eTermKind.String, builtIn, name, arguments);

                case eBuiltIn.Label:
                    return new FormulaNode.BuiltIn(
                        call.Start,
                        TermKinds.WithListness(eTermKind.String, isList),
                        builtIn,
                        name,
                        arguments
                    );
            }

            // What's left takes a number, or a list of them, first: trunc, floor, ceil, round, fixed, and number
            if (!TermKinds.IsNumber(first))
            {
                return this.Fail(
                    call.Arguments[0].Start,
                    $"{name}() works on numbers, and {What(call.Arguments[0])}"
                        + $" is {TermKinds.Describe(first)} ({At(call.Arguments[0].Start)})."
                );
            }

            if (builtIn is eBuiltIn.Number or eBuiltIn.Abs)
            {
                return new FormulaNode.BuiltIn(call.Start, first, builtIn, name, arguments);
            }

            if (builtIn == eBuiltIn.Clamp)
            {
                return this.CheckClamp(call, name, arguments);
            }

            if (arguments.Length == 1)
            {
                return new FormulaNode.BuiltIn(
                    call.Start,
                    TermKinds.WithListness(eTermKind.Int, isList),
                    builtIn,
                    name,
                    arguments
                );
            }

            if (!this.CheckWholeNumber(call, arguments, 1, $"{name}() wants a whole number of decimal places"))
            {
                return null;
            }

            return builtIn == eBuiltIn.Fixed
                ? new FormulaNode.BuiltIn(call.Start, TermKinds.WithListness(eTermKind.String, isList), builtIn, name, arguments)
                : new FormulaNode.BuiltIn(
                    call.Start,
                    TermKinds.WithListness(eTermKind.Float, isList),
                    eBuiltIn.RoundToPlaces,
                    name,
                    arguments
                );
        }

        // sum, min, and max take one list, or one or more single numbers, and never a mix
        private FormulaNode CheckNumbersOrList (FormulaSyntax.Call call, eBuiltIn builtIn, string name, FormulaNode[] arguments)
        {
            bool isWhole = true;
            for (int index = 0; index < arguments.Length; ++index)
            {
                eTermKind kind = arguments[index].Kind;
                if (TermKinds.IsList(kind) && (arguments.Length > 1))
                {
                    return this.Fail(
                        call.Start,
                        $"{name}() takes one list, or several single numbers, never a mix ({At(call.Start)})."
                    );
                }

                if (!TermKinds.IsNumber(kind))
                {
                    return this.Fail(
                        call.Arguments[index].Start,
                        $"{name}() works on numbers, and {What(call.Arguments[index])}"
                            + $" is {TermKinds.Describe(kind)} ({At(call.Arguments[index].Start)})."
                    );
                }

                isWhole &= TermKinds.ItemOf(kind) == eTermKind.Int;
            }

            // An average can always leave a fraction, even of whole numbers
            eTermKind resultKind = (isWhole && (builtIn != eBuiltIn.Avg)) ? eTermKind.Int : eTermKind.Float;
            return new FormulaNode.BuiltIn(call.Start, resultKind, builtIn, name, arguments);
        }

        // clamp(x, low, high): x can be a list, which is clamped item by item, and low and high are single numbers
        private FormulaNode CheckClamp (FormulaSyntax.Call call, string name, FormulaNode[] arguments)
        {
            bool isWhole = TermKinds.ItemOf(arguments[0].Kind) == eTermKind.Int;
            for (int index = 1; index < arguments.Length; ++index)
            {
                eTermKind kind = arguments[index].Kind;
                if (TermKinds.IsList(kind) || !TermKinds.IsNumber(kind))
                {
                    string which = index == 1 ? "its low" : "its high";
                    return this.Fail(
                        call.Arguments[index].Start,
                        $"{name}() wants a single number for {which}, and {What(call.Arguments[index])}"
                            + $" is {TermKinds.Describe(kind)} ({At(call.Arguments[index].Start)})."
                    );
                }

                isWhole &= kind == eTermKind.Int;
            }

            eTermKind result = TermKinds.WithListness(
                isWhole ? eTermKind.Int : eTermKind.Float,
                TermKinds.IsList(arguments[0].Kind)
            );

            return new FormulaNode.BuiltIn(call.Start, result, eBuiltIn.Clamp, name, arguments);
        }

        private bool CheckWholeNumber (FormulaSyntax.Call call, FormulaNode[] arguments, int index, string wantedClause)
        {
            eTermKind kind = arguments[index].Kind;
            if (kind == eTermKind.Int)
            {
                return true;
            }

            if (kind == eTermKind.Float)
            {
                this.FailFraction(arguments[index], wantedClause);
                return false;
            }

            this.Fail(
                call.Arguments[index].Start,
                $"{wantedClause}, and {What(call.Arguments[index])}"
                    + $" is {TermKinds.Describe(kind)} ({At(call.Arguments[index].Start)})."
            );
            return false;
        }

        // has($x) takes a value's name, which it's always true for when the value isn't optional
        private FormulaNode CheckHas (FormulaSyntax.Call call)
        {
            if (Unwrap(call.Arguments[0]) is not FormulaSyntax.Variable variable)
            {
                return this.Fail(
                    call.Arguments[0].Start,
                    $"has() takes a value's name, like has($modifier), at {At(call.Arguments[0].Start)}."
                );
            }

            if (m_slotByName.ContainsKey(variable.Name))
            {
                return new FormulaNode.HasCheck(call.Start, hostSlot: -1);
            }

            if (
                (m_declarations != null)
                && m_declarations.TryFind(variable.Name, m_ignoreNameCase, out TermDeclarations.Entry declared)
            )
            {
                return new FormulaNode.HasCheck(call.Start, declared.IsOptional ? this.HostSlotFor(declared) : -1);
            }

            // Not a name anything knows, which reading it explains
            return this.CheckVariable(variable);
        }

        /// <summary>
        /// The refusal for calling <paramref name="builtIn"/> with <paramref name="count"/> arguments, or null when that's a
        /// count it takes. How many a built-in takes never depends on kinds, so a table function's body is checked for this
        /// when it's registered.
        /// </summary>
        internal static string WrongArgumentCount (eBuiltIn builtIn, int count, int callIndex)
        {
            bool isCountRight = builtIn switch
            {
                eBuiltIn.Sum or eBuiltIn.Min or eBuiltIn.Max or eBuiltIn.Avg => count >= 1,
                eBuiltIn.Clamp => count == 3,
                eBuiltIn.Highest or eBuiltIn.Lowest or eBuiltIn.Fixed => count == 2,
                eBuiltIn.Join or eBuiltIn.Round => count is 1 or 2,
                _ => count == 1,
            };

            return isCountRight
                ? null
                : $"{CanonicalName(builtIn)}() takes {Usage(builtIn)}, and is given {Plural(count, "argument")},"
                    + $" at {At(callIndex)}.";
        }

        // What made a function's argument a single value where it wants a list, by name when it can: count($pool >= 8)
        //  with $pool a single number is "$pool is a single whole number", since that's what the author has to change
        private static string DescribeSingleArgument (FormulaSyntax syntax, FormulaNode node)
        {
            if (Unwrap(syntax) is FormulaSyntax.Variable variable)
            {
                return $"${variable.Name} is {TermKinds.DescribeSingle(node.Kind)}";
            }

            if (TryFindSingleRead(node, out string name, out eTermKind kind))
            {
                return $"${name} is {TermKinds.DescribeSingle(kind)}";
            }

            return $"its argument is {TermKinds.DescribeSingle(node.Kind)}";
        }

        private static bool TryFindSingleRead (FormulaNode node, out string name, out eTermKind kind)
        {
            switch (node)
            {
                case FormulaNode.HostRead hostRead when !TermKinds.IsList(hostRead.Kind):
                    name = hostRead.Name;
                    kind = hostRead.Kind;
                    return true;

                case FormulaNode.SlotRead slotRead when !TermKinds.IsList(slotRead.Kind):
                    name = slotRead.Name;
                    kind = slotRead.Kind;
                    return true;

                case FormulaNode.Chain chain:
                    foreach (FormulaNode operand in chain.Operands)
                    {
                        if (TryFindSingleRead(operand, out name, out kind))
                        {
                            return true;
                        }
                    }

                    break;

                case FormulaNode.Logic logic:
                    foreach (FormulaNode operand in logic.Operands)
                    {
                        if (TryFindSingleRead(operand, out name, out kind))
                        {
                            return true;
                        }
                    }

                    break;

                case FormulaNode.Negate negate:
                    return TryFindSingleRead(negate.Operand, out name, out kind);

                case FormulaNode.Not not:
                    return TryFindSingleRead(not.Operand, out name, out kind);

                case FormulaNode.Widen widen:
                    return TryFindSingleRead(widen.Inner, out name, out kind);
            }

            name = null;
            kind = default;
            return false;
        }

        internal static string CanonicalName (eBuiltIn builtIn)
        {
            return builtIn switch
            {
                eBuiltIn.Sum => "sum",
                eBuiltIn.Min => "min",
                eBuiltIn.Max => "max",
                eBuiltIn.Count => "count",
                eBuiltIn.Highest => "highest",
                eBuiltIn.Lowest => "lowest",
                eBuiltIn.Join => "join",
                eBuiltIn.Trunc => "trunc",
                eBuiltIn.Floor => "floor",
                eBuiltIn.Ceil => "ceil",
                eBuiltIn.Fixed => "fixed",
                eBuiltIn.Label => "label",
                eBuiltIn.Number => "number",
                eBuiltIn.Has => "has",
                eBuiltIn.Clamp => "clamp",
                eBuiltIn.Abs => "abs",
                eBuiltIn.Avg => "avg",
                eBuiltIn.Any => "any",
                eBuiltIn.All => "all",
                _ => "round",
            };
        }

        private static string Usage (eBuiltIn builtIn)
        {
            return builtIn switch
            {
                eBuiltIn.Sum or eBuiltIn.Min or eBuiltIn.Max or eBuiltIn.Avg => "one list, or one or more numbers",
                eBuiltIn.Clamp => "a number and the low and high to keep it between",
                eBuiltIn.Any or eBuiltIn.All => "one list",
                eBuiltIn.Count => "one list",
                eBuiltIn.Highest or eBuiltIn.Lowest => "a list and how many to keep",
                eBuiltIn.Join => "a list, and maybe text to put between the items",
                eBuiltIn.Round => "a number, and maybe how many decimal places",
                eBuiltIn.Fixed => "a number and how many decimal places",
                eBuiltIn.Has => "one value's name, like has($modifier)",
                _ => "one value",
            };
        }
    }
}
