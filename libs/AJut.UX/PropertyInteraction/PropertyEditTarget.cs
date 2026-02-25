namespace AJut.UX.PropertyInteraction
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using AJut;
    using AJut.Storage;

    public class PropertyEditTarget : ObservableTreeNode
    {
        public delegate object GetValue ();
        public delegate void SetValue (object value);

        private readonly GetValue m_getValue;
        private readonly SetValue? m_setValue;

        public PropertyEditTarget (string propertyPathTarget, GetValue getValue, SetValue? setValue = null)
        {
            this.PropertyPathTarget = propertyPathTarget;
            m_getValue = getValue;
            m_setValue = setValue;
        }

        public string PropertyPathTarget { get; }

        public bool IsReadOnly => m_setValue == null;
        public string[] AdditionalEvalTargets { get; set; }

        private bool m_isExpandable;
        public bool IsExpandable
        {
            get => m_isExpandable;
            set
            {
                if (this.SetAndRaiseIfChanged(ref m_isExpandable, value))
                {
                    // Keep IObservableTreeNode.CanHaveChildren in sync so FlatTreeItem
                    // shows/hides the expand toggle correctly.
                    this.CanHaveChildren = value;
                }
            }
        }

        private string m_editor;
        public string Editor
        {
            get => m_editor;
            set => this.SetAndRaiseIfChanged(ref m_editor, value);
        }

        private string m_displayName;
        public string DisplayName
        {
            get => m_displayName;
            set => this.SetAndRaiseIfChanged(ref m_displayName, value);
        }

        private object m_editValue;
        public object EditValue
        {
            get => m_editValue;
            set
            {
                if (!this.IsReadOnly && this.SetAndRaiseIfChanged(ref m_editValue, value))
                {
                    m_setValue(value);
                }
            }
        }

        private object m_editContext;
        public object EditContext
        {
            get => m_editContext;
            set => this.SetAndRaiseIfChanged(ref m_editContext, value);
        }

        public void RecacheEditValue ()
        {
            this.SetEditValue(m_getValue());
        }

        public bool SetEditValue (object editValue)
        {
            return this.SetAndRaiseIfChanged(ref m_editValue, editValue, nameof(EditValue));
        }

        public bool ShouldEvaluateFor (string propertyPath)
        {
            return this.PropertyPathTarget == propertyPath
                || (this.AdditionalEvalTargets?.Contains(propertyPath) ?? true);
        }

        public override int GetHashCode ()
        {
            return this.PropertyPathTarget.GetHashCode();
        }

        /// <summary>
        /// Generates PropertyEditTarget nodes for every public, settable property on <paramref name="sourceItem"/>.
        /// For complex reference-type properties whose current value is non-null, child targets are recursively
        /// generated and attached so the property tree can be expanded in a FlatTreeListControl.
        /// </summary>
        public static IEnumerable<PropertyEditTarget> GenerateForPropertiesOf (object sourceItem)
        {
            return GenerateForPropertiesOf(sourceItem, depth: 0);
        }

        private static IEnumerable<PropertyEditTarget> GenerateForPropertiesOf (object sourceItem, int depth)
        {
            foreach (PropertyInfo prop in _GetRelevantProperties(sourceItem))
            {
                var editorAttr = prop.GetAttributes<PGEditorAttribute>().FirstOrDefault();
                string displayName = prop.GetAttributes<DisplayNameAttribute>().FirstOrDefault()?.DisplayName;
                string[] aliases = prop.GetAttributes<PGAltPropertyAliasAttribute>().FirstOrDefault()?.AltPropertyAliases;

                var target = new PropertyEditTarget(prop.Name, () => prop.GetValue(sourceItem), (v) => prop.SetValue(sourceItem, v))
                {
                    DisplayName = displayName ?? prop.Name.ConvertToFriendlyEn(),
                    Editor = editorAttr?.Editor ?? prop.PropertyType.Name,
                    AdditionalEvalTargets = aliases,
                };

                // Recurse into complex reference types (non-string, non-enum, non-value-type) that
                // have a non-null value and editable sub-properties. Cap recursion at depth 5.
                if (editorAttr == null && depth < 5 && _IsComplexObjectType(prop.PropertyType))
                {
                    object subValue = prop.GetValue(sourceItem);
                    if (subValue != null && _GetRelevantProperties(subValue).Any())
                    {
                        target.IsExpandable = true;
                        foreach (PropertyEditTarget child in GenerateForPropertiesOf(subValue, depth + 1))
                        {
                            child.Setup();
                            target.InsertChild(target.Children.Count, child);
                        }
                    }
                }

                yield return target;
            }

            IEnumerable<PropertyInfo> _GetRelevantProperties (object _item)
            {
                bool showReadOnly = _item.GetType().IsTaggedWithAttribute<PGShowReadonlyAttribute>();
                return _item.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty).Where(_Filter);

                bool _Filter (PropertyInfo _prop)
                {
                    if (_prop.IsTaggedWithAttribute<PGHiddenAttribute>())
                    {
                        return false;
                    }

                    if (showReadOnly)
                    {
                        return true;
                    }

                    return _prop.SetMethod != null || _prop.IsTaggedWithAttribute<PGShowReadonlyAttribute>();
                }
            }
        }

        private static bool _IsComplexObjectType (Type type)
        {
            return !type.IsValueType
                && type != typeof(string)
                && !type.IsEnum
                && !type.IsArray
                && !typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
        }

        internal void TakeOn (PropertyEditTarget target)
        {
            // Intentionally empty — merging of duplicate targets not yet implemented.
        }

        internal void Setup ()
        {
            if (this.DisplayName.IsNullOrEmpty())
            {
                this.DisplayName = this.PropertyPathTarget.ConvertToFriendlyEn();
            }

            if (this.EditValue == null)
            {
                m_editValue = m_getValue?.Invoke();
            }
        }
    }
}
