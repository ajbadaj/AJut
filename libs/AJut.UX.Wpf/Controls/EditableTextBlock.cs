namespace AJut.UX.Controls
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Threading;
    using DPUtils = DPUtils<EditableTextBlock>;

    public enum eEditTextInitializationBehavior
    {
        SelectAll,
        CursorAtEnd,
    }

    public enum eEditTextCloseAction
    {
        Cancel,
        Apply,
    }

    public enum eEditTextInstigator
    {
        Manually,
        DoubleClick,
        MouseOver,
    }

    public class EditableTextBlock : Control
    {
        private bool m_isManuallyEscapingEdit;
        private TextBox m_editTextBox;
        private bool m_dontAutoFocusNextEdit;

        // ========================[ Construction ]==============================
        static EditableTextBlock ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(EditableTextBlock), new FrameworkPropertyMetadata(typeof(EditableTextBlock)));
        }

        public override void OnApplyTemplate ()
        {
            this.OnIsEditingChanged();
        }

        // ===================[ Dependency Properties ]==========================
        public static readonly DependencyProperty IsEditingProperty = DPUtils.Register(_ => _.IsEditing, (d, e) => d.OnIsEditingChanged());
        public bool IsEditing
        {
            get => (bool)this.GetValue(IsEditingProperty);
            set => this.SetValue(IsEditingProperty, value);
        }

        private static readonly DependencyPropertyKey IsEmptyPropertyKey = DPUtils.RegisterReadOnly(_ => _.IsEmpty, true);
        public static readonly DependencyProperty IsEmptyProperty = IsEmptyPropertyKey.DependencyProperty;
        public bool IsEmpty
        {
            get => (bool)this.GetValue(IsEmptyProperty);
            protected set => this.SetValue(IsEmptyPropertyKey, value);
        }

        public static readonly DependencyProperty TextProperty = DPUtils.RegisterFP(_ => _.Text, (d, e) => d.IsEmpty = String.IsNullOrEmpty(e.NewValue), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault);
        public string Text
        {
            get => (string)this.GetValue(TextProperty);
            set => this.SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty NoTextSetMessageProperty = DPUtils.Register(_ => _.NoTextSetMessage, "No text set");
        public string NoTextSetMessage
        {
            get => (string)this.GetValue(NoTextSetMessageProperty);
            set => this.SetValue(NoTextSetMessageProperty, value);
        }

        public static readonly DependencyProperty IsReadOnlyProperty = DPUtils.Register(_ => _.IsReadOnly);
        public bool IsReadOnly
        {
            get => (bool)this.GetValue(IsReadOnlyProperty);
            set => this.SetValue(IsReadOnlyProperty, value);
        }

        public static readonly DependencyProperty IsReadOnlyCaretVisibleProperty = DPUtils.Register(_ => _.IsReadOnlyCaretVisible, true);
        public bool IsReadOnlyCaretVisible
        {
            get => (bool)this.GetValue(IsReadOnlyCaretVisibleProperty);
            set => this.SetValue(IsReadOnlyCaretVisibleProperty, value);
        }

        public static readonly DependencyProperty EditTextInstagatorActionProperty = DPUtils.Register(_ => _.EditTextInstagatorAction, eEditTextInstigator.DoubleClick);
        public eEditTextInstigator EditTextInstagatorAction
        {
            get => (eEditTextInstigator)this.GetValue(EditTextInstagatorActionProperty);
            set => this.SetValue(EditTextInstagatorActionProperty, value);
        }

        public static readonly DependencyProperty EditTextInitializationBehaviorProperty = DPUtils.Register(_ => _.EditTextInitializationBehavior);
        public eEditTextInitializationBehavior EditTextInitializationBehavior
        {
            get => (eEditTextInitializationBehavior)this.GetValue(EditTextInitializationBehaviorProperty);
            set => this.SetValue(EditTextInitializationBehaviorProperty, value);
        }

        public static readonly DependencyProperty LostFocusEditCloseProperty = DPUtils.Register(_ => _.LostFocusEditClose, eEditTextCloseAction.Apply);
        public eEditTextCloseAction LostFocusEditClose
        {
            get => (eEditTextCloseAction)this.GetValue(LostFocusEditCloseProperty);
            set => this.SetValue(LostFocusEditCloseProperty, value);
        }

        private static readonly DependencyPropertyKey PreviousTextPropertyKey = DPUtils.RegisterReadOnly(_ => _.PreviousText);
        public static readonly DependencyProperty PreviousTextProperty = PreviousTextPropertyKey.DependencyProperty;
        public string PreviousText
        {
            get => (string)this.GetValue(PreviousTextProperty);
            protected set => this.SetValue(PreviousTextPropertyKey, value);
        }

        private static readonly DependencyPropertyKey EditTextPropertyKey = DPUtils.RegisterReadOnly(_ => _.EditText);
        public static readonly DependencyProperty EditTextProperty = EditTextPropertyKey.DependencyProperty;
        public string EditText
        {
            get => (string)this.GetValue(EditTextProperty);
            protected set => this.SetValue(EditTextPropertyKey, value);
        }


        private static readonly DependencyPropertyKey StartedEditingEmptyPropertyKey = DPUtils.RegisterReadOnly(_ => _.StartedEditingEmpty);
        public static readonly DependencyProperty StartedEditingEmptyProperty = StartedEditingEmptyPropertyKey.DependencyProperty;
        public bool StartedEditingEmpty
        {
            get => (bool)this.GetValue(StartedEditingEmptyProperty);
            protected set => this.SetValue(StartedEditingEmptyPropertyKey, value);
        }


        public static readonly DependencyProperty TextBlockTextTrimmingProperty = DPUtils.Register(_ => _.TextBlockTextTrimming, TextTrimming.None);
        public TextTrimming TextBlockTextTrimming
        {
            get => (TextTrimming)this.GetValue(TextBlockTextTrimmingProperty);
            set => this.SetValue(TextBlockTextTrimmingProperty, value);
        }

        public static readonly DependencyProperty TextBlockTextWrappingProperty = DPUtils.Register(_ => _.TextBlockTextWrapping, TextWrapping.NoWrap);
        public TextWrapping TextBlockTextWrapping
        {
            get => (TextWrapping)this.GetValue(TextBlockTextWrappingProperty);
            set => this.SetValue(TextBlockTextWrappingProperty, value);
        }

        private void OnIsEditingChanged ()
        {
            if (this.IsEditing)
            {
                this.PreviousText = this.Text;
                if (this.EditText != null)
                {
                    this.Text = this.EditText;
                }

                this.StartedEditingEmpty = this.Text.IsNullOrEmpty();

                this.MouseDoubleClick -= _InitiateEditing;
                this.MouseEnter -= _InitiateEditingWithoutFocus;
                if (this.EditTextInstagatorAction.HasFlag(eEditTextInstigator.MouseOver))
                {
                    this.MouseLeave -= _StopEditing;
                    this.MouseLeave += _StopEditing;
                }

                this.KeyDown -= _OnKeyDown;
                this.KeyDown += _OnKeyDown;

                this.Dispatcher.InvokeAsync(() =>
                {
                    if (m_editTextBox != null)
                    {
                        m_editTextBox.LostFocus -= _EditTextBoxLostFocus;
                        m_editTextBox = null;
                    }
                    m_editTextBox = this.GetFirstChildOf<TextBox>();
                    if (!m_dontAutoFocusNextEdit || m_editTextBox.Focus())
                    {
                        m_editTextBox.LostFocus += _EditTextBoxLostFocus;
                        var window = Window.GetWindow(this);
                        if (window != null)
                        {
                            window.Deactivated -= _EditTextBoxLostFocus;
                            window.Deactivated += _EditTextBoxLostFocus;
                        }
                    }

                    if (this.EditTextInitializationBehavior == eEditTextInitializationBehavior.SelectAll)
                    {
                        m_editTextBox.SelectAll();
                    }
                    else if (this.EditTextInitializationBehavior == eEditTextInitializationBehavior.CursorAtEnd)
                    {
                        m_editTextBox.CaretIndex = this.Text?.Length ?? 0;
                    }
                }, DispatcherPriority.Input);
            }
            else
            {
                if (!m_isManuallyEscapingEdit)
                {
                    // We're doing a normal commit, setting the edit text to null will allow it to be reset on next edit
                    this.EditText = null;
                    this.PreviousText = null;
                }

                this.MouseDoubleClick -= _InitiateEditing;
                this.MouseEnter -= _InitiateEditingWithoutFocus;
                this.MouseLeave -= _StopEditing;

                if (this.EditTextInstagatorAction.HasFlag(eEditTextInstigator.DoubleClick))
                {
                    this.MouseDoubleClick += _InitiateEditing;
                }
                if (this.EditTextInstagatorAction.HasFlag(eEditTextInstigator.MouseOver))
                {
                    this.MouseEnter += _InitiateEditingWithoutFocus;
                }

                this.KeyDown -= _OnKeyDown;

                if (m_editTextBox != null)
                {
                    m_editTextBox.LostFocus -= _EditTextBoxLostFocus;
                    m_editTextBox = null;

                    var window = Window.GetWindow(this);
                    if (window != null)
                    {
                        window.Deactivated -= _EditTextBoxLostFocus;
                    }
                }
            }

            void _InitiateEditingWithoutFocus (object sender, EventArgs e)
            {
                m_dontAutoFocusNextEdit = true;
                try
                {
                    this.IsEditing = true;
                }
                finally { m_dontAutoFocusNextEdit = false; }
            }
            void _InitiateEditing (object sender, EventArgs e) => this.IsEditing = true;
            void _StopEditing (object sender, EventArgs e)
            {
                if (!(m_editTextBox?.IsKeyboardFocusWithin ?? false))
                {
                    this.IsEditing = false;
                }
            }

            void _OnKeyDown (object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    _CancelEdit();
                }
                else if (e.Key == Key.Return)
                {
                    this.IsEditing = false;
                }
            }

            void _EditTextBoxLostFocus (object sender, EventArgs e)
            {
                if (m_editTextBox != null)
                {
                    m_editTextBox.LostFocus -= _EditTextBoxLostFocus;
                }

                if (this.LostFocusEditClose == eEditTextCloseAction.Cancel)
                {
                    _CancelEdit();
                }
                else
                {
                    this.IsEditing = false;
                }
            }

            void _CancelEdit ()
            {
                m_isManuallyEscapingEdit = true;
                try
                {
                    // Save the edit text
                    this.EditText = this.Text;
                    this.Text = this.PreviousText;
                    this.IsEditing = false;
                }
                catch { }
                finally { m_isManuallyEscapingEdit = false; }
            }
        }

        protected override void OnPreviewKeyUp (KeyEventArgs e)
        {
            if (!this.IsEditing && e.Key == Key.Return)
            {
                this.IsEditing = true;
            }
            base.OnPreviewKeyUp(e);
        }
    }
}