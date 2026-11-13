namespace AJut.Text.AJson
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The members to write on top of a value's defaults, from <see cref="JsonIncludeReadOnlyInWriteAttribute"/>'s names, as a
    /// tree: each node is one member name, and its children are the names below it ("Bounds.Right" is Right under Bounds). A
    /// writer carries the node for the value it is writing on that value's <see cref="JsonBuilder"/>, and each member it writes
    /// gets the child of the same name.
    /// </summary>
    internal sealed class ReadOnlyWriteIncludes
    {
        private readonly Dictionary<string, ReadOnlyWriteIncludes> m_children = new Dictionary<string, ReadOnlyWriteIncludes>(StringComparer.Ordinal);
        private readonly List<string> m_order = new List<string>();
        private bool m_isAllowedMissing;

        // ===============================[ Setup/Construction/Teardown ]===========================
        private ReadOnlyWriteIncludes (string path)
        {
            this.Path = path;
        }

        /// <summary>
        /// The tree for a set of member names, or null when there are none
        /// </summary>
        /// <param name="memberNames">The attribute's names, dotted to reach further down</param>
        /// <param name="allowsDerivedTypes">
        /// True for <see cref="eAllowedProperties.DerivedTypes"/>: any of these names may be on a subtype only, so a value without
        /// one is expected, and <see cref="IsAllowedMissing"/> says so for every name in the tree
        /// </param>
        public static ReadOnlyWriteIncludes Parse (IEnumerable<string> memberNames, bool allowsDerivedTypes)
        {
            if (memberNames == null)
            {
                return null;
            }

            ReadOnlyWriteIncludes root = new ReadOnlyWriteIncludes(String.Empty);
            foreach (string memberName in memberNames)
            {
                if (String.IsNullOrWhiteSpace(memberName))
                {
                    continue;
                }

                ReadOnlyWriteIncludes node = root;
                foreach (string part in memberName.Split('.'))
                {
                    string name = part.Trim();
                    if (name.Length == 0)
                    {
                        break;
                    }

                    node = node.GetOrAddChild(name);
                    node.m_isAllowedMissing = allowsDerivedTypes;
                }
            }

            return root.m_order.Count == 0 ? null : root;
        }

        /// <summary>
        /// Both sets of names in one tree: a member's own names, and the names its parent's value gave it. Either may be null.
        /// </summary>
        public static ReadOnlyWriteIncludes Merge (ReadOnlyWriteIncludes first, ReadOnlyWriteIncludes second)
        {
            if (first == null)
            {
                return second;
            }

            if (second == null || ReferenceEquals(first, second))
            {
                return first;
            }

            ReadOnlyWriteIncludes merged = new ReadOnlyWriteIncludes(first.Path);
            merged.MergeIn(first);
            merged.MergeIn(second);
            return merged;
        }

        // ===============================[ Properties ]===========================
        /// <summary>
        /// The dotted name this node stands for, from the top of the attribute it came from, for the log
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// The member names directly below this node, in the order they were given
        /// </summary>
        public IReadOnlyList<string> Names => m_order;

        // ===============================[ Public Interface Methods ]===========================
        /// <summary>
        /// The names below <paramref name="memberName"/>, or null when it has none of its own (or is not one of these names)
        /// </summary>
        public ReadOnlyWriteIncludes ChildFor (string memberName)
        {
            return (m_children.TryGetValue(memberName, out ReadOnlyWriteIncludes child) && child.m_order.Count > 0) ? child : null;
        }

        /// <summary>
        /// True when the attribute that gave <paramref name="memberName"/> passed <see cref="eAllowedProperties.DerivedTypes"/>,
        /// so a value that has no member of that name is expected (it isn't the subtype that does) and isn't worth a log. A name
        /// that more than one attribute gave is allowed missing only when every one of them said so.
        /// </summary>
        public bool IsAllowedMissing (string memberName)
        {
            return m_children.TryGetValue(memberName, out ReadOnlyWriteIncludes child) && child.m_isAllowedMissing;
        }

        /// <summary>
        /// The dotted name of <paramref name="memberName"/> below this node, for the log
        /// </summary>
        public string PathTo (string memberName) => this.Path.Length == 0 ? memberName : $"{this.Path}.{memberName}";

        // ===============================[ Helper Methods ]===========================
        private ReadOnlyWriteIncludes GetOrAddChild (string name)
        {
            if (!m_children.TryGetValue(name, out ReadOnlyWriteIncludes child))
            {
                child = new ReadOnlyWriteIncludes(this.PathTo(name));
                m_children.Add(name, child);
                m_order.Add(name);
            }

            return child;
        }

        private void MergeIn (ReadOnlyWriteIncludes source)
        {
            foreach (string name in source.m_order)
            {
                ReadOnlyWriteIncludes from = source.m_children[name];
                bool isNew = !m_children.ContainsKey(name);
                ReadOnlyWriteIncludes child = this.GetOrAddChild(name);
                child.m_isAllowedMissing = isNew ? from.m_isAllowedMissing : (child.m_isAllowedMissing && from.m_isAllowedMissing);
                child.MergeIn(from);
            }
        }
    }
}
