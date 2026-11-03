namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using AJut.UX.CodeBinding;
    using AJut.UX.Helpers;
    using AJut.UX.PropertyInteraction;
    using AJut.UX.Theming;
    using UIKit;

    /// <summary>
    /// Builds the editor view for one property row. <paramref name="target"/> is the row's effective editor target
    /// (read and write its EditValue); bind through <paramref name="bindings"/>, which is disposed when the row goes.
    /// <paramref name="isReadOnly"/> is true when the editor must not take edits: the property has no setter, or the
    /// grid is read-only. UIKit has no enabled state that flows down to child views, so the editor applies it itself.
    /// </summary>
    public delegate UIView PropertyEditorFactory (PropertyEditTarget target, BindingSet bindings, bool isReadOnly);

    /// <summary>Where a property row puts its editor</summary>
    public enum ePropertyEditorPlacement
    {
        /// <summary>Under the property's name, taking the row's whole width: most editors</summary>
        BelowLabel,

        /// <summary>At the end of the name's line, for a compact editor such as a switch, the way a settings form sets one out</summary>
        BesideLabel,
    }

    // ===========[ PropertyGridEditorRegistry ]=================================
    // The UIKit counterpart of PropertyGridTemplateSelector: editor factories keyed by a target's
    // editor key (its property type name, or a [PGEditor] key), each with where its row puts it. A
    // key with no registered factory gets the built-in editor for it if there is one (Nullable,
    // Button, List; see PropertyGridRowRules), otherwise Default.
    //
    // Like the WinUI3 and AppKit grids, nothing beyond those three is registered unless asked:
    // RegisterStandardEditors adds editors for strings, booleans, numbers, ColorArgb colors, and enums,
    // built from UIKit's own controls and AJut's UIKit controls, for a grid that should just work.
    public sealed class PropertyGridEditorRegistry
    {
        private static readonly string[] kNumericTypeNames = new[]
        {
            nameof(Byte), nameof(SByte), nameof(Int16), nameof(UInt16), nameof(Int32), nameof(UInt32),
            nameof(Int64), nameof(UInt64), nameof(Single), nameof(Double), nameof(Decimal),
        };

        private readonly Dictionary<string, Registration> m_factories = new Dictionary<string, Registration>();

        // ===========[ Properties ]============================================
        /// <summary>The editor for a key with nothing registered and no built-in; null shows no editor</summary>
        public PropertyEditorFactory Default { get; set; }

        /// <summary>Where a row puts the <see cref="Default"/> editor</summary>
        public ePropertyEditorPlacement DefaultPlacement { get; set; } = ePropertyEditorPlacement.BelowLabel;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Registers (or replaces) the editor for an editor key, and where its row puts it</summary>
        public void Register (string editorKey, PropertyEditorFactory factory, ePropertyEditorPlacement placement = ePropertyEditorPlacement.BelowLabel)
        {
            m_factories[editorKey] = new Registration(factory, placement);
        }

        public bool Unregister (string editorKey) => m_factories.Remove(editorKey);

        public bool IsRegistered (string editorKey) => m_factories.ContainsKey(editorKey);

        public bool TryGet (string editorKey, out PropertyEditorFactory factory)
        {
            if (m_factories.TryGetValue(editorKey, out Registration registration))
            {
                factory = registration.Factory;
                return true;
            }

            factory = null;
            return false;
        }

        /// <summary>Where a row puts the editor registered for a key, or the <see cref="DefaultPlacement"/> for a key with none</summary>
        public ePropertyEditorPlacement GetPlacement (string editorKey)
        {
            return m_factories.TryGetValue(editorKey, out Registration registration) ? registration.Placement : this.DefaultPlacement;
        }

        /// <summary>
        /// Registers editors for the common value types, and sets <see cref="Default"/> to one that edits enums with an
        /// EnumComboBox and shows anything else as read-only text. Keys already registered are kept. A boolean's switch and a
        /// color's swatch sit beside the property's name; the rest go under it.
        /// </summary>
        public void RegisterStandardEditors ()
        {
            this.RegisterIfMissing(nameof(String), CreateStringEditor, ePropertyEditorPlacement.BelowLabel);
            this.RegisterIfMissing(nameof(Boolean), CreateBooleanEditor, ePropertyEditorPlacement.BesideLabel);
            this.RegisterIfMissing(nameof(ColorArgb), CreateColorEditor, ePropertyEditorPlacement.BesideLabel);
            foreach (string typeName in kNumericTypeNames)
            {
                this.RegisterIfMissing(typeName, CreateNumericEditor, ePropertyEditorPlacement.BelowLabel);
            }

            if (this.Default == null)
            {
                this.Default = CreateFallbackEditor;
            }
        }

        // ===========[ Helper Methods ]========================================
        private void RegisterIfMissing (string editorKey, PropertyEditorFactory factory, ePropertyEditorPlacement placement)
        {
            if (!m_factories.ContainsKey(editorKey))
            {
                m_factories.Add(editorKey, new Registration(factory, placement));
            }
        }

        private static UIView CreateStringEditor (PropertyEditTarget target, BindingSet bindings, bool isReadOnly)
        {
            // A disabled text field keeps its text color, so a read-only one takes the disabled text color itself
            var field = new UITextField
            {
                BorderStyle = UITextBorderStyle.RoundedRect,
                Enabled = !isReadOnly,
                TextColor = ThemedColors.Get(isReadOnly ? ThemedColorKeys.TextControlForegroundDisabled : ThemedColorKeys.TextControlForeground),
                ClearButtonMode = UITextFieldViewMode.WhileEditing,
                AutocorrectionType = UITextAutocorrectionType.No,
                AutocapitalizationType = UITextAutocapitalizationType.None,
                ReturnKeyType = UIReturnKeyType.Done,
                ShouldReturn = DismissKeyboard,
            };

            // Themed, the normal AJut text box, the same as a stock text field beside the grid
            StockControlTint.ApplyToOwnTextField(field);

            // Commit on Return or leaving the field, the way a XAML TextBox's default binding does, rather than writing
            // the source on every keystroke
            bindings.BindControl(
                field, UIControlEvent.EditingDidEnd | UIControlEvent.EditingDidEndOnExit,
                ReadFieldText, WriteFieldText,
                target, nameof(PropertyEditTarget.EditValue), _Read, _Write
            );
            return field;

            string _Read () => target.EditValue as string ?? string.Empty;
            void _Write (string value) => target.EditValue = value;
        }

        private static UIView CreateBooleanEditor (PropertyEditTarget target, BindingSet bindings, bool isReadOnly)
        {
            var toggle = new UISwitch { Enabled = !isReadOnly };
            bindings.BindOn(toggle, target, nameof(PropertyEditTarget.EditValue), _Read, _Write);
            return toggle;

            bool _Read () => target.EditValue is bool isOn && isOn;
            void _Write (bool value) => target.EditValue = value;
        }

        // Every non-XAML AJut stack edits colors as ColorArgb under the "ColorArgb" key, so a consumer with its own color
        // type aliases it to ColorArgb once ([PGTypeAlias]) and gets this editor on each of them
        private static UIView CreateColorEditor (PropertyEditTarget target, BindingSet bindings, bool isReadOnly)
        {
            var swatch = new ColorEditIngressControl { IsReadOnly = isReadOnly };
            bindings.BindEditColor(swatch, target, nameof(PropertyEditTarget.EditValue), _Read, _Write);
            return swatch;

            ColorArgb _Read () => target.EditValue is ColorArgb color ? color : default;
            void _Write (ColorArgb value) => target.EditValue = value;
        }

        private static UIView CreateNumericEditor (PropertyEditTarget target, BindingSet bindings, bool isReadOnly)
        {
            var editor = new NumericEditor { IsReadOnly = isReadOnly };
            bindings.BindValue<object>(editor, target, nameof(PropertyEditTarget.EditValue), _Read, _Write);
            return editor;

            object _Read () => target.EditValue;
            void _Write (object value) => target.EditValue = value;
        }

        private static UIView CreateFallbackEditor (PropertyEditTarget target, BindingSet bindings, bool isReadOnly)
        {
            if (target.EditValue is Enum)
            {
                var comboBox = new EnumComboBox { Enabled = !isReadOnly };
                bindings.BindControl(
                    comboBox, UIControlEvent.ValueChanged,
                    ReadSelectedItem, WriteSelectedItem,
                    target, nameof(PropertyEditTarget.EditValue), _Read, _Write
                );
                return comboBox;
            }

            var label = new UILabel
            {
                LineBreakMode = UILineBreakMode.TailTruncation,
                TextColor = ThemedColors.Get(isReadOnly ? ThemedColorKeys.TextControlForegroundDisabled : ThemedColorKeys.TextControlForeground),
            };
            bindings.BindLabel(label, target, nameof(PropertyEditTarget.EditValue), _ReadText);
            return label;

            object _Read () => target.EditValue;
            void _Write (object value) => target.EditValue = value;
            string _ReadText () => target.EditValue?.ToString() ?? string.Empty;
        }

        // Static, so the field holds nothing of the row it is in
        private static bool DismissKeyboard (UITextField field)
        {
            field.ResignFirstResponder();
            return true;
        }

        private static string ReadFieldText (UITextField field) => field.Text ?? string.Empty;
        private static void WriteFieldText (UITextField field, string value) => field.Text = value;
        private static object ReadSelectedItem (EnumComboBox comboBox) => comboBox.SelectedItem;
        private static void WriteSelectedItem (EnumComboBox comboBox, object value) => comboBox.SelectedItem = value;

        // ===========[ Subclasses/structs ]====================================
        private sealed record Registration (PropertyEditorFactory Factory, ePropertyEditorPlacement Placement);
    }
}
