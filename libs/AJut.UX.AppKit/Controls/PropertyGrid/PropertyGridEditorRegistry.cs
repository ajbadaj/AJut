namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using AJut.UX.CodeBinding;
    using AJut.UX.Helpers;
    using AJut.UX.PropertyInteraction;
    using AppKit;

    /// <summary>
    /// Builds the editor view for one property row. <paramref name="target"/> is the row's effective editor target
    /// (read and write its EditValue); bind through <paramref name="bindings"/>, which is disposed when the row goes.
    /// <paramref name="isReadOnly"/> is true when the editor must not take edits: the property has no setter, or the
    /// grid is read-only. AppKit has no enabled state that flows down to child views, so the editor applies it itself.
    /// </summary>
    public delegate NSView PropertyEditorFactory (PropertyEditTarget target, BindingSet bindings, bool isReadOnly);

    // ===========[ PropertyGridEditorRegistry ]=================================
    // The AppKit counterpart of PropertyGridTemplateSelector: editor factories keyed by a target's
    // editor key (its property type name, or a [PGEditor] key). A key with no registered factory
    // gets the built-in editor for it if there is one (Nullable, Button, List; see
    // PropertyGridRowRules), otherwise Default.
    //
    // Like the WinUI3 grid, nothing beyond those three is registered unless asked:
    // RegisterStandardEditors adds editors for strings, booleans, numbers, ColorArgb colors and enums, built from
    // AJut's AppKit controls, for a grid that should just work.
    public sealed class PropertyGridEditorRegistry
    {
        private static readonly string[] kNumericTypeNames = new[]
        {
            nameof(Byte), nameof(SByte), nameof(Int16), nameof(UInt16), nameof(Int32), nameof(UInt32),
            nameof(Int64), nameof(UInt64), nameof(Single), nameof(Double), nameof(Decimal),
        };

        private readonly Dictionary<string, PropertyEditorFactory> m_factories = new Dictionary<string, PropertyEditorFactory>();

        // ===========[ Properties ]============================================
        /// <summary>The editor for a key with nothing registered and no built-in; null shows no editor</summary>
        public PropertyEditorFactory Default { get; set; }

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Registers (or replaces) the editor for an editor key</summary>
        public void Register (string editorKey, PropertyEditorFactory factory)
        {
            m_factories[editorKey] = factory;
        }

        public bool Unregister (string editorKey) => m_factories.Remove(editorKey);

        public bool IsRegistered (string editorKey) => m_factories.ContainsKey(editorKey);

        public bool TryGet (string editorKey, out PropertyEditorFactory factory) => m_factories.TryGetValue(editorKey, out factory);

        /// <summary>
        /// Registers editors for the common value types, and sets <see cref="Default"/> to one that edits enums
        /// with an EnumComboBox and shows anything else as read-only text. Keys already registered are kept.
        /// </summary>
        public void RegisterStandardEditors ()
        {
            this.RegisterIfMissing(nameof(String), CreateStringEditor);
            this.RegisterIfMissing(nameof(Boolean), CreateBooleanEditor);
            this.RegisterIfMissing(nameof(ColorArgb), CreateColorEditor);
            foreach (string typeName in kNumericTypeNames)
            {
                this.RegisterIfMissing(typeName, CreateNumericEditor);
            }

            if (this.Default == null)
            {
                this.Default = CreateFallbackEditor;
            }
        }

        // ===========[ Helper Methods ]========================================
        private void RegisterIfMissing (string editorKey, PropertyEditorFactory factory)
        {
            if (!m_factories.ContainsKey(editorKey))
            {
                m_factories.Add(editorKey, factory);
            }
        }

        private static NSView CreateStringEditor (PropertyEditTarget target, BindingSet bindings, bool isReadOnly)
        {
            var field = new NSTextField { Editable = !isReadOnly };

            // Commit on Return or leaving the field, the way a XAML TextBox's default binding does,
            // rather than writing the source on every keystroke
            bindings.BindText(field, target, nameof(PropertyEditTarget.EditValue), _Read, _Write, commitOnly: true);
            return field;

            string _Read () => target.EditValue as string ?? String.Empty;
            void _Write (string value) => target.EditValue = value;
        }

        private static NSView CreateBooleanEditor (PropertyEditTarget target, BindingSet bindings, bool isReadOnly)
        {
            var check = new NSButton { Title = String.Empty, Enabled = !isReadOnly };
            check.SetButtonType(NSButtonType.Switch);
            bindings.BindChecked(check, target, nameof(PropertyEditTarget.EditValue), _Read, _Write);
            return check;

            bool _Read () => target.EditValue is bool isOn && isOn;
            void _Write (bool value) => target.EditValue = value;
        }

        // Every non-XAML AJut stack edits colors as ColorArgb under the "ColorArgb" key, so a consumer with its
        // own color type aliases it to ColorArgb once ([PGTypeAlias]) and gets this editor on each of them
        private static NSView CreateColorEditor (PropertyEditTarget target, BindingSet bindings, bool isReadOnly)
        {
            var swatch = new ColorEditIngressControl { IsReadOnly = isReadOnly };
            bindings.BindEditColor(swatch, target, nameof(PropertyEditTarget.EditValue), _Read, _Write);
            return swatch;

            ColorArgb _Read () => target.EditValue is ColorArgb color ? color : default;
            void _Write (ColorArgb value) => target.EditValue = value;
        }

        private static NSView CreateNumericEditor (PropertyEditTarget target, BindingSet bindings, bool isReadOnly)
        {
            var editor = new NumericEditor { IsReadOnly = isReadOnly };
            bindings.BindValue(editor, target, nameof(PropertyEditTarget.EditValue), _Read, _Write);
            return editor;

            object _Read () => target.EditValue;
            void _Write (object value) => target.EditValue = value;
        }

        private static NSView CreateFallbackEditor (PropertyEditTarget target, BindingSet bindings, bool isReadOnly)
        {
            if (target.EditValue is Enum)
            {
                var comboBox = new EnumComboBox { Enabled = !isReadOnly };
                bindings.BindSelectedItem(comboBox, target, nameof(PropertyEditTarget.EditValue), _Read, _Write);
                return comboBox;
            }

            NSTextField label = NSTextField.CreateLabel(String.Empty);
            label.LineBreakMode = NSLineBreakMode.TruncatingTail;
            bindings.BindLabel(label, target, nameof(PropertyEditTarget.EditValue), _ReadText);
            return label;

            object _Read () => target.EditValue;
            void _Write (object value) => target.EditValue = value;
            string _ReadText () => target.EditValue?.ToString() ?? String.Empty;
        }
    }
}
