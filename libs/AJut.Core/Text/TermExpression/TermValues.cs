namespace AJut.Text
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The values for one evaluation, one for each name its <see cref="TermDeclarations"/> declares, made with
    /// <see cref="TermDeclarations.CreateValues"/>. An optional value can be left out; any other value left out is an
    /// evaluation error when a formula reads it.
    /// </summary>
    /// <remarks>
    /// Setting a name that isn't declared, or a value of the wrong kind, throws, since that's a bug in the host rather than a
    /// mistake by whoever typed the formula. A whole number is accepted where a number is declared.
    /// </remarks>
    public sealed class TermValues
    {
        private readonly TermValue[] m_values;
        private readonly bool[] m_isSet;

        // ===========[ Setup/Construction/Teardown ]==========================

        internal TermValues (TermDeclarations declarations)
        {
            this.Declarations = declarations;
            m_values = new TermValue[declarations.Count];
            m_isSet = new bool[declarations.Count];
        }

        // ===========[ Properties ]===========================================

        /// <summary>
        /// What these are values for.
        /// </summary>
        public TermDeclarations Declarations { get; }

        // ===========[ Public Interface Methods ]=============================

        /// <exception cref="ArgumentException">When <paramref name="name"/> isn't declared, or isn't declared a whole number or
        /// a number</exception>
        public TermValues Set (string name, int value) => this.Set(name, (long)value);

        /// <exception cref="ArgumentException">When <paramref name="name"/> isn't declared, or isn't declared a whole number or
        /// a number</exception>
        public TermValues Set (string name, long value) => this.Set(name, TermValue.Int(value));

        /// <exception cref="ArgumentException">When <paramref name="name"/> isn't declared a number</exception>
        public TermValues Set (string name, double value) => this.Set(name, TermValue.Float(value));

        /// <exception cref="ArgumentException">When <paramref name="name"/> isn't declared true or false</exception>
        public TermValues Set (string name, bool value) => this.Set(name, TermValue.Bool(value));

        /// <exception cref="ArgumentException">When <paramref name="name"/> isn't declared text</exception>
        public TermValues Set (string name, string value) => this.Set(name, TermValue.String(value));

        /// <summary>
        /// Sets a value as it is, label and all: a single value, or a list made with <see cref="TermValue.List"/>.
        /// </summary>
        /// <exception cref="ArgumentException">When <paramref name="name"/> isn't declared as the value's kind, or the value
        /// is an error</exception>
        public TermValues Set (string name, TermValue value)
        {
            TermDeclarations.Entry entry = this.Find(name);
            if (value.IsError || !TermKinds.Accepts(entry.Kind, value.Kind))
            {
                throw new ArgumentException(
                    $"'{name}' is declared {entry.Kind}, and can't be set to {value.DescribeForHost()}",
                    nameof(value)
                );
            }

            value = value.AsKind(entry.Kind);
            if (entry.Print != null)
            {
                value = value.IsList ? WithItemPrint(value, entry.Print) : value.WithPrint(entry.Print);
            }

            m_values[entry.Index] = value;
            m_isSet[entry.Index] = true;
            return this;
        }

        /// <summary>
        /// Sets a list of whole numbers, like a set of scores.
        /// </summary>
        public TermValues Set (string name, IEnumerable<int> values)
            => this.SetList(name, values, eTermKind.Int, value => TermValue.Int(value));

        /// <summary>
        /// Sets a list of whole numbers.
        /// </summary>
        public TermValues Set (string name, IEnumerable<long> values)
            => this.SetList(name, values, eTermKind.Int, value => TermValue.Int(value));

        /// <summary>
        /// Sets a list of numbers.
        /// </summary>
        public TermValues Set (string name, IEnumerable<double> values)
            => this.SetList(name, values, eTermKind.Float, value => TermValue.Float(value));

        /// <summary>
        /// Sets a list of true or false values.
        /// </summary>
        public TermValues Set (string name, IEnumerable<bool> values)
            => this.SetList(name, values, eTermKind.Bool, value => TermValue.Bool(value));

        /// <summary>
        /// Sets a list of texts.
        /// </summary>
        public TermValues Set (string name, IEnumerable<string> values)
            => this.SetList(name, values, eTermKind.String, value => TermValue.String(value));

        /// <summary>
        /// Sets a list from values that each keep their own label, like a Yes/No pool:
        /// <c>values.Set("d", new[] { TermValue.Int(1, "Yes"), TermValue.Int(0, "No") })</c>.
        /// </summary>
        public TermValues Set (string name, IEnumerable<TermValue> values)
        {
            ArgumentNullException.ThrowIfNull(values);
            TermDeclarations.Entry entry = this.Find(name);
            if (!TermKinds.IsList(entry.Kind))
            {
                throw new ArgumentException($"'{name}' is declared {entry.Kind}, not a list", nameof(name));
            }

            return this.Set(name, TermValue.List(TermKinds.ItemOf(entry.Kind), values));
        }

        /// <summary>
        /// Whether <paramref name="name"/> has a value.
        /// </summary>
        public bool IsSet (string name)
        {
            TermDeclarations.Entry entry = this.Find(name);
            return m_isSet[entry.Index];
        }

        /// <summary>
        /// The value <paramref name="name"/> was set to, when it's declared and set.
        /// </summary>
        public bool TryGetValue (string name, out TermValue value)
        {
            if (
                !this.Declarations.TryFind(name ?? String.Empty, ignoreCase: false, out TermDeclarations.Entry entry)
                || !m_isSet[entry.Index]
            )
            {
                value = default;
                return false;
            }

            value = m_values[entry.Index];
            return true;
        }

        // ===========[ Internal Interface Methods ]===========================

        internal bool IsSetAt (int index) => m_isSet[index];

        internal TermValue GetAt (int index) => m_values[index];

        /// <summary>
        /// Sets what a formula worked out, which is already the declared kind and carries its own print rules
        /// </summary>
        internal void SetComputed (int index, TermValue value)
        {
            m_values[index] = value;
            m_isSet[index] = true;
        }

        // ===========[ Helpers ]==============================================

        private TermDeclarations.Entry Find (string name)
        {
            if ((name == null) || !this.Declarations.TryFind(name, ignoreCase: false, out TermDeclarations.Entry entry))
            {
                throw new ArgumentException($"'{name}' isn't declared", nameof(name));
            }

            return entry;
        }

        private TermValues SetList<T> (string name, IEnumerable<T> values, eTermKind itemKind, Func<T, TermValue> make)
        {
            ArgumentNullException.ThrowIfNull(values);
            TermDeclarations.Entry entry = this.Find(name);
            if (!TermKinds.IsList(entry.Kind) || !TermKinds.Accepts(TermKinds.ItemOf(entry.Kind), itemKind))
            {
                throw new ArgumentException(
                    $"'{name}' is declared {entry.Kind}, and can't be set to a list of {itemKind}",
                    nameof(values)
                );
            }

            var items = new List<TermValue>();
            foreach (T value in values)
            {
                items.Add(make(value));
            }

            return this.Set(name, TermValue.List(itemKind, items));
        }

        private static TermValue WithItemPrint (TermValue list, Func<TermValue, string> print)
        {
            var items = new TermValue[list.Items.Length];
            for (int index = 0; index < items.Length; ++index)
            {
                items[index] = list.Items[index].WithPrint(print);
            }

            return TermValue.Trusted(list.Kind, items);
        }
    }
}
