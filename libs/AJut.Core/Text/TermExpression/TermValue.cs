namespace AJut.Text
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text;

    /// <summary>
    /// One value a formula works with or gives back: a whole number, a number, true or false, text, or a list of one of those.
    /// A value can carry a label, which is what it shows when that isn't its number, like a rating of 1 that reads "Bronze", or
    /// an hour of 0 that reads "00".
    /// </summary>
    /// <remarks>
    /// <para>Printing shows the label when there is one, and math and comparisons use the number. Anything computed from a
    /// value (arithmetic, sum, rounding) has no label. <see cref="ToString"/> prints the way a template does.</para>
    /// <para>Never changes once made, including a list's items, so it can be shared freely.</para>
    /// </remarks>
    public readonly struct TermValue
    {
        private readonly double m_number;
        private readonly string m_text;
        private readonly TermValue[] m_items;
        private readonly Func<TermValue, string> m_print;
        private readonly bool m_isError;

        // ===========[ Setup/Construction/Teardown ]==========================

        private TermValue (
            eTermKind kind,
            double number,
            string text,
            TermValue[] items,
            string label,
            Func<TermValue, string> print,
            bool isError
        )
        {
            this.Kind = kind;
            this.Label = label;
            m_number = number;
            m_text = text;
            m_items = items;
            m_print = print;
            m_isError = isError;
        }

        /// <summary>
        /// A whole number, with an optional label to show in its place.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">When <paramref name="value"/> is past 2^53 either way, which a formula
        /// can't hold exactly</exception>
        public static TermValue Int (long value, string label = null)
        {
            if (!FormulaNumbers.IsExactWhole(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "A formula can't hold a whole number past 2^53 exactly"
                );
            }

            return new TermValue(eTermKind.Int, value, null, null, label, null, isError: false);
        }

        /// <summary>
        /// A number that can have a fraction, with an optional label to show in its place.
        /// </summary>
        public static TermValue Float (double value, string label = null)
        {
            return new TermValue(eTermKind.Float, value, null, null, label, null, isError: false);
        }

        /// <summary>
        /// True or false, with an optional label to show in its place.
        /// </summary>
        public static TermValue Bool (bool value, string label = null)
        {
            return new TermValue(eTermKind.Bool, value ? 1 : 0, null, null, label, null, isError: false);
        }

        /// <summary>
        /// Text.
        /// </summary>
        public static TermValue String (string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return new TermValue(eTermKind.String, 0, value, null, null, null, isError: false);
        }

        /// <summary>
        /// A list of <paramref name="itemKind"/> values, each keeping its label. Whole numbers are accepted in a list of
        /// numbers.
        /// </summary>
        /// <exception cref="ArgumentException">When <paramref name="itemKind"/> is a list kind, or an item isn't
        /// <paramref name="itemKind"/></exception>
        public static TermValue List (eTermKind itemKind, IEnumerable<TermValue> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            if (!TermKinds.IsKnown(itemKind) || TermKinds.IsList(itemKind))
            {
                throw new ArgumentException($"A list's items can't be {itemKind}", nameof(itemKind));
            }

            var copied = new List<TermValue>();
            foreach (TermValue item in items)
            {
                if (item.IsError || !TermKinds.Accepts(itemKind, item.Kind))
                {
                    throw new ArgumentException($"A list of {itemKind} can't hold {item.Kind}", nameof(items));
                }

                copied.Add(item.AsKind(itemKind));
            }

            return Trusted(TermKinds.ListOf(itemKind), copied.ToArray());
        }

        /// <summary>
        /// How a host function says it failed. The message is shown to whoever typed the formula, at the call, so write it in
        /// their words.
        /// </summary>
        public static TermValue Error (string message)
        {
            ArgumentNullException.ThrowIfNull(message);
            return new TermValue(eTermKind.Int, 0, message, null, null, null, isError: true);
        }

        // ===========[ Properties ]===========================================

        /// <summary>
        /// What this value is.
        /// </summary>
        public eTermKind Kind { get; }

        /// <summary>
        /// What this value shows when it's printed, in place of its number, or null when it has no label. Only ever on a single
        /// value: a list's items carry their own.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Whether this is a host function's failure (<see cref="Error"/>) rather than a value.
        /// </summary>
        public bool IsError => m_isError;

        /// <summary>
        /// The message of an <see cref="Error"/>, or null for a value.
        /// </summary>
        public string ErrorMessage => m_isError ? m_text : null;

        /// <summary>
        /// Whether this is a list.
        /// </summary>
        public bool IsList => !m_isError && TermKinds.IsList(this.Kind);

        /// <summary>
        /// The whole number, as a long.
        /// </summary>
        /// <exception cref="InvalidOperationException">When this isn't <see cref="eTermKind.Int"/></exception>
        public long AsInt
        {
            get
            {
                this.ThrowUnless(this.Kind == eTermKind.Int, eTermKind.Int);
                return (long)m_number;
            }
        }

        /// <summary>
        /// The number, from either a whole number or a number with a fraction.
        /// </summary>
        /// <exception cref="InvalidOperationException">When this isn't <see cref="eTermKind.Int"/> or
        /// <see cref="eTermKind.Float"/></exception>
        public double AsFloat
        {
            get
            {
                this.ThrowUnless(this.Kind is eTermKind.Int or eTermKind.Float, eTermKind.Float);
                return m_number;
            }
        }

        /// <summary>
        /// True or false.
        /// </summary>
        /// <exception cref="InvalidOperationException">When this isn't <see cref="eTermKind.Bool"/></exception>
        public bool AsBool
        {
            get
            {
                this.ThrowUnless(this.Kind == eTermKind.Bool, eTermKind.Bool);
                return m_number != 0;
            }
        }

        /// <summary>
        /// The text. To print any value the way a template would, use <see cref="ToString"/> instead.
        /// </summary>
        /// <exception cref="InvalidOperationException">When this isn't <see cref="eTermKind.String"/></exception>
        public string AsString
        {
            get
            {
                this.ThrowUnless(this.Kind == eTermKind.String, eTermKind.String);
                return m_text;
            }
        }

        /// <summary>
        /// A list's items, each with its own label.
        /// </summary>
        /// <exception cref="InvalidOperationException">When this isn't a list</exception>
        public IReadOnlyList<TermValue> AsList
        {
            get
            {
                if (!this.IsList)
                {
                    throw new InvalidOperationException($"This value is {this.DescribeForHost()}, not a list");
                }

                return new ReadOnlyCollection<TermValue>(m_items);
            }
        }

        /// <summary>
        /// The number behind any value but text: a bool is 1 or 0
        /// </summary>
        internal double Number => m_number;

        internal TermValue[] Items => m_items;

        // ===========[ Public Interface Methods ]=============================

        /// <summary>
        /// Prints the value the way a template does: the label if there is one, otherwise the host's print rule if it declared
        /// one, otherwise the number (culture invariant, whole numbers with no decimal point), true or false, or the text. A
        /// list prints its items joined with ", ".
        /// </summary>
        public override string ToString ()
        {
            if (m_isError)
            {
                return m_text;
            }

            var output = new StringBuilder();
            this.PrintTo(output);
            return output.ToString();
        }

        // ===========[ Internal Interface Methods ]===========================

        /// <summary>
        /// Wraps an array nothing else holds on to, without copying it
        /// </summary>
        internal static TermValue Trusted (eTermKind listKind, TermValue[] items)
        {
            return new TermValue(listKind, items.Length, null, items, null, null, isError: false);
        }

        /// <summary>
        /// A number or a bool (1 or 0) that was computed, so it has no label and no print rule
        /// </summary>
        internal static TermValue Computed (eTermKind kind, double number)
        {
            return new TermValue(kind, number, null, null, null, null, isError: false);
        }

        /// <summary>
        /// The same value with nothing on it about how it shows: no label and no print rule. What anything computed
        /// gets.
        /// </summary>
        internal TermValue Bare ()
        {
            return new TermValue(this.Kind, m_number, m_text, m_items, null, null, m_isError);
        }

        internal TermValue WithPrint (Func<TermValue, string> print)
        {
            return new TermValue(this.Kind, m_number, m_text, m_items, this.Label, print, m_isError);
        }

        /// <summary>
        /// The same value as kind, which it has to be accepted as: an int as a float, or a list of ints as a list of
        /// floats, keeping every label
        /// </summary>
        internal TermValue AsKind (eTermKind kind)
        {
            if (kind == this.Kind)
            {
                return this;
            }

            if (!TermKinds.IsList(kind))
            {
                return new TermValue(kind, m_number, m_text, null, this.Label, m_print, isError: false);
            }

            eTermKind itemKind = TermKinds.ItemOf(kind);
            var items = new TermValue[m_items.Length];
            for (int index = 0; index < items.Length; ++index)
            {
                items[index] = m_items[index].AsKind(itemKind);
            }

            return Trusted(kind, items);
        }

        internal void PrintTo (StringBuilder output)
        {
            if (this.Label != null)
            {
                output.Append(this.Label);
            }
            else if (m_print != null)
            {
                output.Append(m_print(this));
            }
            else
            {
                this.PrintPlainTo(output);
            }
        }

        /// <summary>
        /// Prints as if there were no label or print rule. A list's items still print their own way.
        /// </summary>
        internal void PrintPlainTo (StringBuilder output)
        {
            switch (this.Kind)
            {
                case eTermKind.Int:
                case eTermKind.Float:
                    output.Append(FormulaNumbers.Format(m_number));
                    break;

                case eTermKind.Bool:
                    output.Append(m_number != 0 ? "true" : "false");
                    break;

                case eTermKind.String:
                    output.Append(m_text);
                    break;

                default:
                    for (int index = 0; index < m_items.Length; ++index)
                    {
                        if (index > 0)
                        {
                            output.Append(", ");
                        }

                        m_items[index].PrintTo(output);
                    }

                    break;
            }
        }

        internal string DescribeForHost () => m_isError ? "an error" : this.Kind.ToString();

        // ===========[ Helpers ]==============================================

        private void ThrowUnless (bool isKind, eTermKind asked)
        {
            if (m_isError || !isKind)
            {
                throw new InvalidOperationException($"This value is {this.DescribeForHost()}, not {asked}");
            }
        }
    }
}
