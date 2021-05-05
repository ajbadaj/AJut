namespace AJut.Application.Controls
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
        private readonly SetValue m_setValue;

        static PropertyEditTarget ()
        {
            //TreeTraversal<PropertyEditTarget>.SetupDefaults();
        }

        public PropertyEditTarget (string propertyPathTarget, GetValue getValue, SetValue setValue = null)
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
            set => this.SetAndRaiseIfChanged(ref m_isExpandable, value);
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

        public static IEnumerable<PropertyEditTarget> GenerateForPropertiesOf (object sourceItem)
        {
            foreach (PropertyInfo prop in _GetRelevantProperties(sourceItem))
            {
                var editor = prop.GetAttributes<PGEditorAttribute>().FirstOrDefault();
                string displayName = prop.GetAttributes<DisplayNameAttribute>().FirstOrDefault()?.DisplayName;
                string[] aliases = prop.GetAttributes<PGAltPropertyAliasAttribute>().FirstOrDefault()?.AltPropertyAliases;
                yield return new PropertyEditTarget(prop.Name, () => prop.GetValue(sourceItem), (v) => prop.SetValue(sourceItem, v))
                {
                    DisplayName = displayName ?? prop.Name.ConvertToFriendlyEn(),
                    Editor = editor?.Editor ?? prop.PropertyType.Name,
                    AdditionalEvalTargets = aliases,
                };
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

        // TODO: Next is to add source

        internal void TakeOn (PropertyEditTarget target)
        {
            //throw new NotImplementedException();
        }

        internal void Setup ()
        {
            if (this.DisplayName.IsNullOrEmpty())
            {
                this.DisplayName = this.PropertyPathTarget.ConvertToFriendlyEn();
            }

            if (this.EditValue == null)
            {
                m_editValue = m_getValue();
            }
        }
    }
}
