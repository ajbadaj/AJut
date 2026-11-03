namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using AJut.UX.CodeBinding;
    using AJut.UX.Helpers;
    using AJut.UX.PropertyInteraction;
    using AJut.UX.Theming;
    using Android.Content;
    using Android.Text;
    using Android.Views;
    using Android.Views.InputMethods;
    using Android.Widget;

    /// <summary>
    /// Builds the editor view for one property row, in <paramref name="context"/>. <paramref name="target"/> is the row's
    /// effective editor target (read and write its EditValue); bind through <paramref name="bindings"/>, which is disposed
    /// when the row goes. <paramref name="isReadOnly"/> is true when the editor must not take edits: the property has no
    /// setter, or the grid is read-only. A ViewGroup's Enabled never reaches its children, so the editor applies it itself.
    /// </summary>
    public delegate View? PropertyEditorFactory (Context context, PropertyEditTarget target, BindingSet bindings, bool isReadOnly);

    /// <summary>Where a property row puts its editor</summary>
    public enum ePropertyEditorPlacement
    {
        /// <summary>Under the property's name, taking the row's whole width: most editors</summary>
        BelowLabel,

        /// <summary>At the end of the name's line, for a compact editor such as a switch, the way a settings screen sets one out</summary>
        BesideLabel,
    }

    // ===========[ PropertyGridEditorRegistry ]=================================
    // The Android counterpart of PropertyGridTemplateSelector: editor factories keyed by a target's
    // editor key (its property type name, or a [PGEditor] key), each with where its row puts it. A
    // key with no registered factory gets the built-in editor for it if there is one (Nullable,
    // Button, List; see PropertyGridRowRules), otherwise Default.
    //
    // Like the WinUI3, AppKit, and UIKit grids, nothing beyond those three is registered unless asked:
    // RegisterStandardEditors adds editors for strings, booleans, numbers, ColorArgb colors, and enums,
    // built from the SDK's own widgets and AJut's Android controls, for a grid that should just work.
    public sealed class PropertyGridEditorRegistry
    {
        private static readonly string[] kNumericTypeNames = new[]
        {
            nameof(Byte), nameof(SByte), nameof(Int16), nameof(UInt16), nameof(Int32), nameof(UInt32),
            nameof(Int64), nameof(UInt64), nameof(Single), nameof(Double), nameof(Decimal),
        };

        // A text editor's colors take the AJut palette with the theme loaded; unthemed it keeps the app's own field look
        private static readonly ThemedColor kTextForeground = ThemedColor.FromArgb("TextControlForeground", 0xE4000000, 0xFFFFFFFF);
        private static readonly ThemedColor kTextForegroundDisabled = ThemedColor.FromArgb("TextControlForegroundDisabled", 0x5C000000, 0x5DFFFFFF);

        private readonly Dictionary<string, Registration> m_factories = new Dictionary<string, Registration>();

        // ===========[ Properties ]============================================
        /// <summary>The editor for a key with nothing registered and no built-in; null shows no editor</summary>
        public PropertyEditorFactory? Default { get; set; }

        /// <summary>Where a row puts the <see cref="Default"/> editor</summary>
        public ePropertyEditorPlacement DefaultPlacement { get; set; } = ePropertyEditorPlacement.BelowLabel;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Registers (or replaces) the editor for an editor key, and where its row puts it</summary>
        public void Register (string editorKey, PropertyEditorFactory factory, ePropertyEditorPlacement placement = ePropertyEditorPlacement.BelowLabel)
        {
            m_factories[editorKey] = new Registration(factory, placement);
        }

        /// <summary>Removes the editor registered for an editor key; false when there was none</summary>
        public bool Unregister (string editorKey) => m_factories.Remove(editorKey);

        /// <summary>Whether an editor is registered for an editor key (a built-in or Default editor doesn't count)</summary>
        public bool IsRegistered (string editorKey) => m_factories.ContainsKey(editorKey);

        /// <summary>The editor registered for an editor key; false when there is none</summary>
        public bool TryGet (string editorKey, [NotNullWhen(true)] out PropertyEditorFactory? factory)
        {
            if (m_factories.TryGetValue(editorKey, out Registration? registration))
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
            return m_factories.TryGetValue(editorKey, out Registration? registration) ? registration.Placement : this.DefaultPlacement;
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

        private static View CreateStringEditor (Context context, PropertyEditTarget target, BindingSet bindings, bool isReadOnly)
        {
            // A disabled field already dims its text unthemed; with the theme loaded it is the normal AJut text box, the
            // same as a stock text field beside the grid, its disabled text color for a read-only one
            var field = new EditText(context)
            {
                Enabled = !isReadOnly,
                InputType = InputTypes.ClassText,
                ImeOptions = ImeAction.Done,
                SaveEnabled = false,
            };

            field.SetSingleLine(true);
            StockControlTint.ApplyToOwnTextField(field);

            // Commit on Done or leaving the field, the way a XAML TextBox's default binding does, rather than writing the
            // source on every keystroke
            BindingSet.TwoWayChannel channel = bindings.TwoWay<string>(
                target, nameof(PropertyEditTarget.EditValue),
                _Read, _Write,
                _ReadField, _WriteField
            );

            bindings.Add(new TextCommitLink(field, channel));
            return field;

            string _Read () => target.EditValue as string ?? string.Empty;
            void _Write (string value) => target.EditValue = value;
            string _ReadField () => field.Text ?? string.Empty;
            void _WriteField (string value) => field.Text = value;
        }

        private static View CreateBooleanEditor (Context context, PropertyEditTarget target, BindingSet bindings, bool isReadOnly)
        {
            var toggle = new Switch(context) { Enabled = !isReadOnly };
            bindings.BindChecked(toggle, target, nameof(PropertyEditTarget.EditValue), _Read, _Write);
            return toggle;

            bool _Read () => target.EditValue is bool isOn && isOn;
            void _Write (bool value) => target.EditValue = value;
        }

        // Every non-XAML AJut stack edits colors as ColorArgb under the "ColorArgb" key, so a consumer with its own color
        // type aliases it to ColorArgb once ([PGTypeAlias]) and gets this editor on each of them
        private static View CreateColorEditor (Context context, PropertyEditTarget target, BindingSet bindings, bool isReadOnly)
        {
            var swatch = new ColorEditIngressControl(context) { IsReadOnly = isReadOnly };
            bindings.BindEditColor(swatch, target, nameof(PropertyEditTarget.EditValue), _Read, _Write);
            return swatch;

            ColorArgb _Read () => target.EditValue is ColorArgb color ? color : default;
            void _Write (ColorArgb value) => target.EditValue = value;
        }

        private static View CreateNumericEditor (Context context, PropertyEditTarget target, BindingSet bindings, bool isReadOnly)
        {
            var editor = new NumericEditor(context) { IsReadOnly = isReadOnly };
            bindings.BindView<NumericEditor, EventArgs<object>, object>(
                editor,
                HookValueChanged,
                UnhookValueChanged,
                ReadEditorValue,
                WriteEditorValue,
                target,
                nameof(PropertyEditTarget.EditValue),
                _Read,
                _Write
            );

            return editor;

            object _Read () => target.EditValue!;
            void _Write (object value) => target.EditValue = value;
        }

        private static View CreateFallbackEditor (Context context, PropertyEditTarget target, BindingSet bindings, bool isReadOnly)
        {
            if (target.EditValue is Enum)
            {
                var comboBox = new EnumComboBox(context) { Enabled = !isReadOnly };
                bindings.BindView<EnumComboBox, EventArgs<object?>, object?>(
                    comboBox,
                    HookSelectedItemChanged,
                    UnhookSelectedItemChanged,
                    ReadSelectedItem,
                    WriteSelectedItem,
                    target,
                    nameof(PropertyEditTarget.EditValue),
                    _Read,
                    _Write
                );

                return comboBox;
            }

            var label = new TextView(context)
            {
                Ellipsize = TextUtils.TruncateAt.End,
            };

            label.SetSingleLine(true);
            if (ThemeColors.IsThemeLoaded)
            {
                label.SetTextColor(ThemeColors.Resolve(context, isReadOnly ? kTextForegroundDisabled : kTextForeground).ToAndroidColor());
            }

            bindings.BindLabel(label, target, nameof(PropertyEditTarget.EditValue), _ReadText);
            return label;

            object? _Read () => target.EditValue;
            void _Write (object? value) => target.EditValue = value!;
            string _ReadText () => target.EditValue?.ToString() ?? string.Empty;
        }

        private static void HookValueChanged (NumericEditor editor, EventHandler<EventArgs<object>> handler) => editor.ValueChanged += handler;
        private static void UnhookValueChanged (NumericEditor editor, EventHandler<EventArgs<object>> handler) => editor.ValueChanged -= handler;
        private static object ReadEditorValue (NumericEditor editor) => editor.Value;
        private static void WriteEditorValue (NumericEditor editor, object value) => editor.Value = value;

        private static void HookSelectedItemChanged (EnumComboBox comboBox, EventHandler<EventArgs<object?>> handler) => comboBox.SelectedItemChanged += handler;
        private static void UnhookSelectedItemChanged (EnumComboBox comboBox, EventHandler<EventArgs<object?>> handler) => comboBox.SelectedItemChanged -= handler;
        private static object? ReadSelectedItem (EnumComboBox comboBox) => comboBox.SelectedItem;
        private static void WriteSelectedItem (EnumComboBox comboBox, object? value) => comboBox.SelectedItem = value;

        // ===========[ Subclasses/structs ]====================================
        private sealed record Registration (PropertyEditorFactory Factory, ePropertyEditorPlacement Placement);

        // Writes a text field's edit back when the user is done with it: on the keyboard's Done, which also puts the
        // keyboard away, or on the field losing focus. Its two subscriptions are both undone on Dispose.
        private sealed class TextCommitLink : IDisposable
        {
            private readonly EditText m_field;
            private readonly BindingSet.TwoWayChannel m_channel;

            public TextCommitLink (EditText field, BindingSet.TwoWayChannel channel)
            {
                m_field = field;
                m_channel = channel;
                m_field.FocusChange += this.OnFocusChange;
                m_field.EditorAction += this.OnEditorAction;
            }

            public void Dispose ()
            {
                m_field.FocusChange -= this.OnFocusChange;
                m_field.EditorAction -= this.OnEditorAction;
            }

            private void OnFocusChange (object? sender, View.FocusChangeEventArgs e)
            {
                if (!e.HasFocus)
                {
                    m_channel.OnTargetEdited();
                }
            }

            private void OnEditorAction (object? sender, TextView.EditorActionEventArgs e)
            {
                if (e.ActionId != ImeAction.Done)
                {
                    e.Handled = false;
                    return;
                }

                m_channel.OnTargetEdited();
                var keyboard = m_field.Context?.GetSystemService(Context.InputMethodService) as InputMethodManager;
                keyboard?.HideSoftInputFromWindow(m_field.WindowToken, HideSoftInputFlags.None);
                e.Handled = true;
            }
        }
    }
}
