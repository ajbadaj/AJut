namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Input;
    using System.Windows.Media;
    using AJut;
    using AJut.TypeManagement;
    using DPUtils = DPUtils<NumericEditor>;

    [TemplatePart(Name = nameof(PART_TextArea), Type = typeof(TextBox))]
    public class NumericEditor : Control, IUserEditNotifier
    {
        private TextBox PART_TextArea;
        private bool m_blockValueChangeReentrancy;
        private object m_textEditPreviousData = null;

        // ===========================[ Construction ]============================================
        public NumericEditor ()
        {
            this.DisplayValue = new TextEditNumberViewModel(this, 0f);
            this.CommandBindings.Add(new CommandBinding(NudgeIncreaseCommand, _OnNudgeIncreaseExecuted, _OnCanNudgeLarger));
            this.CommandBindings.Add(new CommandBinding(NudgeDecreaseCommand, _OnNudgeDecreaseExecuted, _OnCanNudgeSmaller));

            void _OnCanNudgeLarger (object sender, CanExecuteRoutedEventArgs e)
            {
                if (this.IsReadOnly)
                {
                    e.CanExecute = false;
                    return;
                }

                if (!this.DisplayValue?.IsAtMaximum ?? false)
                {
                    e.CanExecute = true;
                }
            }

            void _OnCanNudgeSmaller (object sender, CanExecuteRoutedEventArgs e)
            {
                if (this.IsReadOnly)
                {
                    e.CanExecute = false;
                    return;
                }

                if (!this.DisplayValue?.IsAtMinimum ?? false)
                {
                    e.CanExecute = true;
                }
            }

            void _OnNudgeIncreaseExecuted (object sender, RoutedEventArgs e)
            {
                this.NudgeIncrease(notifyOfUserEdit: true);
            }

            void _OnNudgeDecreaseExecuted (object sender, RoutedEventArgs e)
            {
                this.NudgeDecrease(notifyOfUserEdit: true);
            }
        }

        static NumericEditor ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NumericEditor), new FrameworkPropertyMetadata(typeof(NumericEditor)));
        }

        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            if (this.PART_TextArea != null)
            {
                this.PART_TextArea.GotFocus -= _TextAreaGotFocus;
                this.PART_TextArea.LostFocus -= _TextAreaLostFocus;
                this.PART_TextArea.PreviewKeyDown -= _PreviewOnTextAreaKeyDown;
                this.PART_TextArea = null;
            }

            this.PART_TextArea = (TextBox)this.GetTemplateChild(nameof(PART_TextArea));
            this.PART_TextArea.PreviewKeyDown += _PreviewOnTextAreaKeyDown;
            this.PART_TextArea.GotFocus += _TextAreaGotFocus;
            this.PART_TextArea.LostFocus += _TextAreaLostFocus;

            void _PreviewOnTextAreaKeyDown (object _s, KeyEventArgs _e)
            {
                if (this.IsReadOnly)
                {
                    return;
                }

                if (_e.Key == Key.Return)
                {
                    if (this.PerformTextUpdate())
                    {
                        _e.Handled = true;
                    }
                }
                if (_e.Key == Key.Up)
                {
                    this.NudgeIncrease(notifyOfUserEdit: m_textEditPreviousData == null);
                    _e.Handled = true;
                }
                if (_e.Key == Key.Down)
                {
                    this.NudgeDecrease(notifyOfUserEdit: m_textEditPreviousData == null);
                    _e.Handled = true;
                }
            }

            void _TextAreaGotFocus (object sender, RoutedEventArgs e)
            {
                m_textEditPreviousData = this.Value;
            }

            void _TextAreaLostFocus (object _, RoutedEventArgs _e)
            {
                if (this.PerformTextUpdate())
                {
                    _e.Handled = true;
                }
            }
        }

        // ===========================[ Events ]============================================
        /// <summary>
        /// An event that signifies a user edit has completed - this is slightly different than bound or otherwise modified <see cref="Value"/> changes in that this
        /// event signifies: an edit initiation, a single or even several changes, and a completion have all occurred - not just a change. Changes made outside user edit
        /// similarly do not notify via this event.
        /// </summary>
        public event EventHandler<UserEditAppliedEventArgs> UserEditComplete;

        // ===========================[ Commands ]================================================
        public static RoutedCommand NudgeIncreaseCommand = new RoutedCommand(nameof(NudgeIncrease), typeof(NumericEditor));
        public static RoutedCommand NudgeDecreaseCommand = new RoutedCommand(nameof(NudgeDecrease), typeof(NumericEditor));

        // ===========================[ Dependency Properties ]===================================
        public static readonly DependencyProperty ValueProperty = DPUtils.RegisterFP(_ => _.Value, 0.0f, (d, e) => d.OnValueChanged(e.NewValue), (d, e) => d.OnCoerceValue(e), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault);
        public object Value
        {
            get => (object)this.GetValue(ValueProperty);
            set => this.SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty IsReadOnlyProperty = DPUtils.Register(_ => _.IsReadOnly);
        public bool IsReadOnly
        {
            get => (bool)this.GetValue(IsReadOnlyProperty);
            set => this.SetValue(IsReadOnlyProperty, value);
        }

        private static readonly DependencyPropertyKey DisplayValuePropertyKey = DPUtils.RegisterReadOnly(_ => _.DisplayValue, (d, e) => d.OnDisplayValueSet(e.OldValue, e.NewValue));
        public static readonly DependencyProperty DisplayValueProperty = DisplayValuePropertyKey.DependencyProperty;
        public TextEditNumberViewModel DisplayValue
        {
            get => (TextEditNumberViewModel)this.GetValue(DisplayValueProperty);
            protected set => this.SetValue(DisplayValuePropertyKey, value);
        }

        public static readonly DependencyProperty EnforceNumericTypeProperty = DPUtils.Register(_ => _.EnforceNumericType, (d,e)=>d.DisplayValue.ForceType(e.NewValue));
        public Type EnforceNumericType
        {
            get => (Type)this.GetValue(EnforceNumericTypeProperty);
            set => this.SetValue(EnforceNumericTypeProperty, value);
        }

        public static readonly DependencyProperty DecimalPlacesAllowedProperty = DPUtils.Register(_ => _.DecimalPlacesAllowed, -1, (d,e)=>d.DisplayValue?.TryCapTextToMaxDecimalPlaces());
        public int DecimalPlacesAllowed
        {
            get => (int)this.GetValue(DecimalPlacesAllowedProperty);
            set => this.SetValue(DecimalPlacesAllowedProperty, value);
        }

        public static readonly DependencyProperty MinimumProperty = DPUtils.RegisterFP(_ => _.Minimum, null, (d, e) => d.OnCapChanged(), (d, v) => d.OnCoerceBoundaryNumeric(v));
        public object Minimum
        {
            get => this.GetValue(MinimumProperty);
            set => this.SetValue(MinimumProperty, value);
        }

        public static readonly DependencyProperty MaximumProperty = DPUtils.RegisterFP(_ => _.Maximum, null, (d, e) => d.OnCapChanged(), (d,v)=> d.OnCoerceBoundaryNumeric(v));
        public object Maximum
        {
            get => this.GetValue(MaximumProperty);
            set => this.SetValue(MaximumProperty, value);
        }

        private object OnCoerceBoundaryNumeric (object valueAsObj)
        {
            // This is actually handled better elsewhere, ignore
            if (valueAsObj == null)
            {
                return null;
            }

            // If we haven't determined this yet, there's not a ton we can do - keep it as is for now and cast it after
            if (this.DisplayValue.ValueType == null)
            {
                return valueAsObj;
            }

            Type providedValueType = valueAsObj.GetType();

            // Are we parsing a string?
            if (providedValueType == typeof(string))
            {
                if (NumericConversion.TryParseString((string)valueAsObj, this.DisplayValue.ValueType, out dynamic castedValue))
                {
                    return castedValue;
                }
            }

            // Are we parsing a numeric?
            if (NumericConversion.IsSupportedNumericType(providedValueType))
            {
                return NumericConversion.PerformSafeNumericCastToTarget(valueAsObj, providedValueType, out bool _, out bool _);
            }

            // Do fallback
            return null;
        }

        public static readonly DependencyProperty BigNudgeProperty = DPUtils.Register(_ => _.BigNudge, 5.0);
        public double BigNudge
        {
            get => (double)this.GetValue(BigNudgeProperty);
            set => this.SetValue(BigNudgeProperty, value);
        }

        public static readonly DependencyProperty NudgeProperty = DPUtils.Register(_ => _.Nudge, 1.0);
        public double Nudge
        {
            get => (double)this.GetValue(NudgeProperty);
            set => this.SetValue(NudgeProperty, value);
        }

        public static readonly DependencyProperty SmallNudgeProperty = DPUtils.Register(_ => _.SmallNudge, 0.5);
        public double SmallNudge
        {
            get => (double)this.GetValue(SmallNudgeProperty);
            set => this.SetValue(SmallNudgeProperty, value);
        }

        public static readonly DependencyProperty LabelContentProperty = DPUtils.Register(_ => _.LabelContent);
        public object LabelContent
        {
            get => (object)this.GetValue(LabelContentProperty);
            set => this.SetValue(LabelContentProperty, value);
        }

        public static readonly DependencyProperty LabelContentTemplateProperty = DPUtils.Register(_ => _.LabelContentTemplate);
        public DataTemplate LabelContentTemplate
        {
            get => (DataTemplate)this.GetValue(LabelContentTemplateProperty);
            set => this.SetValue(LabelContentTemplateProperty, value);
        }

        public static readonly DependencyProperty LabelForegroundProperty = DPUtils.Register(_ => _.LabelForeground);
        public Brush LabelForeground
        {
            get => (Brush)this.GetValue(LabelForegroundProperty);
            set => this.SetValue(LabelForegroundProperty, value);
        }

        public static readonly DependencyProperty LabelPaddingProperty = DPUtils.Register(_ => _.LabelPadding, new Thickness(0.0));
        public Thickness LabelPadding
        {
            get => (Thickness)this.GetValue(LabelPaddingProperty);
            set => this.SetValue(LabelPaddingProperty, value);
        }

        public static readonly DependencyProperty LabelButtonDockProperty = DPUtils.Register(_ => _.LabelButtonDock, Dock.Right);
        public Dock LabelButtonDock
        {
            get => (Dock)this.GetValue(LabelButtonDockProperty);
            set => this.SetValue(LabelButtonDockProperty, value);
        }

        public static readonly DependencyProperty NumberTextAlignmentProperty = DPUtils.Register(_ => _.NumberTextAlignment);
        public TextAlignment NumberTextAlignment
        {
            get => (TextAlignment)this.GetValue(NumberTextAlignmentProperty);
            set => this.SetValue(NumberTextAlignmentProperty, value);
        }

        public static readonly DependencyProperty IncreaseHoverHighlightProperty = DPUtils.Register(_ => _.IncreaseHoverHighlight, Brushes.Blue);
        public Brush IncreaseHoverHighlight
        {
            get => (Brush)this.GetValue(IncreaseHoverHighlightProperty);
            set => this.SetValue(IncreaseHoverHighlightProperty, value);
        }

        public static readonly DependencyProperty IncreasePressedHighlightProperty = DPUtils.Register(_ => _.IncreasePressedHighlight);
        public Brush IncreasePressedHighlight
        {
            get => (Brush)this.GetValue(IncreasePressedHighlightProperty);
            set => this.SetValue(IncreasePressedHighlightProperty, value);
        }

        public static readonly DependencyProperty DecreaseHoverHighlightProperty = DPUtils.Register(_ => _.DecreaseHoverHighlight);
        public Brush DecreaseHoverHighlight
        {
            get => (Brush)this.GetValue(DecreaseHoverHighlightProperty);
            set => this.SetValue(DecreaseHoverHighlightProperty, value);
        }

        public static readonly DependencyProperty DecreasePressedHighlightProperty = DPUtils.Register(_ => _.DecreasePressedHighlight);
        public Brush DecreasePressedHighlight
        {
            get => (Brush)this.GetValue(DecreasePressedHighlightProperty);
            set => this.SetValue(DecreasePressedHighlightProperty, value);
        }

        public static readonly DependencyProperty ErrorBrushProperty = DPUtils.Register(_ => _.ErrorBrush);
        public Brush ErrorBrush
        {
            get => (Brush)this.GetValue(ErrorBrushProperty);
            set => this.SetValue(ErrorBrushProperty, value);
        }

        // ===========================[ Interface Methods ]===================================
        /// <summary>
        /// Get the value in casted storage
        /// </summary>
        public T GetValue<T> () => (T)Convert.ChangeType(this.Value, typeof(T));

        /// <summary>
        /// Increase the value the nudge ammount
        /// </summary>
        /// <param name="includeKeyboardMods">Include the nudge modifiations for big nudge &amp; small nudge</param>
        /// <param name="notifyOfUserEdit">Notify that this nudge was a user edit (default = false)</param>
        public void NudgeIncrease (bool includeKeyboardMods = true, bool notifyOfUserEdit = false)
        {
            if (includeKeyboardMods && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                this.NudgeBy(this.BigNudge, notifyOfUserEdit);
            }
            else if (includeKeyboardMods && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                this.NudgeBy(this.SmallNudge, notifyOfUserEdit);
            }
            else
            {
                this.NudgeBy(this.Nudge, notifyOfUserEdit);
            }
        }

        /// <summary>
        /// Decrease the value the nudge ammount
        /// </summary>
        /// <param name="includeKeyboardMods">Include the nudge modifiations for big nudge &amp; small nudge</param>
        /// <param name="notifyOfUserEdit">Notify that this nudge was a user edit (default = false)</param>
        public void NudgeDecrease (bool includeKeyboardMods = true, bool notifyOfUserEdit = false)
        {
            if (includeKeyboardMods && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                this.NudgeBy(-this.BigNudge, notifyOfUserEdit);
            }
            else if (includeKeyboardMods && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                this.NudgeBy(-this.SmallNudge, notifyOfUserEdit);
            }
            else
            {
                this.NudgeBy(-this.Nudge, notifyOfUserEdit);
            }
        }

        /// <summary>
        /// Nudge by a passed in value (which will be converted to the target type before application)
        /// </summary>
        /// <param name="value">The value to nudge by, will be converted to the target type before application</param>
        private void NudgeBy (dynamic value, bool notifyOfUserEdit)
        {
            if (this.DisplayValue.IsTextInErrorState)
            {
                if (this.DisplayValue.IsAtMaximum)
                {
                    this.DisplayValue.Text = Convert.ChangeType(this.Maximum, this.DisplayValue.ValueType).ToString();
                }
                else
                {
                    this.DisplayValue.Text = Convert.ChangeType(this.Minimum, this.DisplayValue.ValueType).ToString();
                }

                return;
            }

            object oldValue = this.Value;

            // Preserve the caret location
            int caretOffset = this.DisplayValue.Text.Length - this.PART_TextArea.CaretIndex;
            int selectionLength = this.PART_TextArea.SelectionLength;
            bool isAllSelected = selectionLength == this.DisplayValue.Text.Length;

            // If the value is negative, make it positive so we can support unsigned values
            //  and instead set the positive nudge flag to false
            bool positive = true;
            if (value < 0)
            {
                dynamic negatizer = Convert.ChangeType(-1, value.GetType());
                value *= negatizer;
                positive = false;
            }

            // Perform the nudge
            this.DisplayValue.Nudge(positive, Convert.ChangeType(value, this.DisplayValue.ValueType));

            // Preserve the caret location
            caretOffset = MathUtilities.Cap.Within(0, this.PART_TextArea.Text.Length - 1, this.DisplayValue.Text.Length - caretOffset);
            selectionLength = Math.Min(selectionLength, this.DisplayValue.Text.Length - caretOffset);
            if (selectionLength > 0)
            {
                if (isAllSelected)
                {
                    this.PART_TextArea.SelectAll();
                }
                else
                {
                    this.PART_TextArea.Select(caretOffset, selectionLength);
                }
            }
            else
            {
                this.PART_TextArea.CaretIndex = MathUtilities.Cap.Within(0, this.PART_TextArea.Text.Length - 1, caretOffset);
            }

            if (notifyOfUserEdit)
            {
                this.UserEditComplete?.Invoke(this, new UserEditAppliedEventArgs(oldValue, this.Value));
            }
        }

        public bool PerformTextUpdate ()
        {
            if (!this.DisplayValue.TryCapTextToMaxDecimalPlaces())
            {
                // Text was real bad
                return false;
            }

            var binding = BindingOperations.GetBindingExpression(this.PART_TextArea, TextBox.TextProperty);
            if (binding != null)
            {
                binding.UpdateSource();
            }

            object oldValue = m_textEditPreviousData;
            m_textEditPreviousData = this.Value;
            if (!this.Value.Equals(oldValue))
            {
                this.UserEditComplete?.Invoke(this, new UserEditAppliedEventArgs(oldValue, this.Value));
                return true;
            }

            return false;
        }

        // ===========================[ Event Handlers ]===================================
        private void OnCapChanged ()
        {
            if (m_blockValueChangeReentrancy)
            {
                return;
            }

            this.DisplayValue?.ReevaluateCap();
        }

        private void OnValueChanged (object newValue)
        {
            if (m_blockValueChangeReentrancy)
            {
                return;
            }

            try
            {
                m_blockValueChangeReentrancy = true;

                this.DisplayValue = new TextEditNumberViewModel(this, newValue, this.EnforceNumericType);
                if (newValue is string)
                {
                    this.Value = this.DisplayValue.SourceValue;
                }
            }
            finally
            {
                m_blockValueChangeReentrancy = false;
            }
        }

        private object OnCoerceValue (object baseValue)
        {
            if (baseValue is string strCasted)
            {
                if (this.DisplayValue.TryParseString(strCasted, out dynamic castedValue))
                {
                    return castedValue;
                }

                return 0.0f;
            }

            return baseValue;
        }

        private void OnDisplayValueSet (TextEditNumberViewModel oldValue, TextEditNumberViewModel newValue)
        {
            if (oldValue != null)
            {
                oldValue.ValueChanged -= _DisplayValue_OnValueChanged;
            }
            if (newValue != null)
            {
                newValue.ValueChanged -= _DisplayValue_OnValueChanged;
                newValue.ValueChanged += _DisplayValue_OnValueChanged;
            }

            void _DisplayValue_OnValueChanged (object _sender, EventArgs<object> _e)
            {
                m_blockValueChangeReentrancy = true;
                try
                {
                    this.SetCurrentValue(ValueProperty, _e.Value);
                }
                finally
                {
                    m_blockValueChangeReentrancy = false;
                }
            }
        }

        protected override void OnGotFocus (RoutedEventArgs e)
        {
            this.PART_TextArea?.SelectAll();
            this.PART_TextArea?.Focus();
        }
    }

    public class TextEditNumberViewModel : NotifyPropertyChanged
    {
        private readonly NumericEditor m_owner;
        private bool m_isTypeForced;
        private Type m_valueType;

        // =====================[ Construction ]==========================
        public TextEditNumberViewModel (NumericEditor owner, object value, Type enforcedType = null)
        {
            m_owner = owner;

            if (enforcedType != null)
            {
                m_isTypeForced = true;
                m_valueType = enforcedType;
            }

            if (value is string stringValue)
            {
                if (NumericConversion.TryParseString(stringValue, enforcedType ?? typeof(float), out dynamic castedValue))
                {
                    value = castedValue;
                }
            }

            if (value == null)
            {
                if (enforcedType != null)
                {
                    value = Activator.CreateInstance(enforcedType);
                }
                else
                {
                    value = 0.0f;
                }
            }

            if (this.ValueType == null)
            {
                this.ValueType = value.GetType();
            }

            this.SetSourceValueOneWay(value);
            this.SetTextOneWay();
        }

        // =====================[ Events ]================================
        public event EventHandler<EventArgs<object>> ValueChanged;

        // =====================[ Properties ]=============================
        private string m_text;
        public string Text
        {
            get => m_text;
            set
            {
                if (this.SetAndRaiseIfChanged(ref m_text, value))
                {
                    this.ApplyTextChange(updateSourceValue: true);
                }
            }
        }

        private object m_sourceValue;
        public object SourceValue
        {
            get => m_sourceValue;
            set
            {
                if (this.SetSourceValueOneWay(value))
                {
                    this.SetTextOneWay();
                    this.TryCapTextToMaxDecimalPlaces();
                }
            }
        }

        private bool m_isAtMinimum;
        public bool IsAtMinimum
        {
            get => m_isAtMinimum;
            set => this.SetAndRaiseIfChanged(ref m_isAtMinimum, value);
        }

        private bool m_isAtMaximum;
        public bool IsAtMaximum
        {
            get => m_isAtMaximum;
            set => this.SetAndRaiseIfChanged(ref m_isAtMaximum, value);
        }

        private string m_textErrorMessage;
        public string TextErrorMessage
        {
            get => m_textErrorMessage;
            set => this.SetAndRaiseIfChanged(ref m_textErrorMessage, value, nameof(TextErrorMessage), nameof(IsTextInErrorState));
        }

        public bool IsTextInErrorState
        {
            get => m_textErrorMessage != null;
        }

        public Type ValueType
        {
            get => m_valueType;
            private set
            {
                if (!m_isTypeForced)
                {
                    m_valueType = value;
                }
            }
        }

        // =====================[ Methods ]=============================

        public bool TryCapTextToMaxDecimalPlaces()
        {
            Regex digitManagedParse;
            // Wildcard for "however many, idc"
            if (m_owner.DecimalPlacesAllowed < 0)
            {
                // The first number with or without a decimal place
                digitManagedParse = new Regex(@"(-?\d+\.\d+)?(-?\d+)?");
            }
            else if (m_owner.DecimalPlacesAllowed == 0)
            {
                // The first number not including decimals
                digitManagedParse = new Regex(@"(-?\d+)");
            }
            else // > 0
            {
                digitManagedParse = new Regex($@"(-?\d+\.?\d{{0,{m_owner.DecimalPlacesAllowed}}})");
            }

            // Text was real bad
            Match output = digitManagedParse.Match(this.Text);
            if (!output.Success)
            {
                return false;
            }

            this.Text = output.Captures[0].Value;
            return true;
        }

        public void Nudge (bool positive, object nudgeValue)
        {
            this.SourceValue = kNudgers[this.ValueType].Invoke(positive, this.SourceValue, nudgeValue);
        }

        internal void ReevaluateCap ()
        {
            this.SourceValue = this.SourceValue;
        }

        private object RunValueCapping (object value, out bool wasBelowMin, out bool wasAboveMax)
        {
            return kCappers[this.ValueType](value, m_owner.Minimum, m_owner.Maximum, out wasBelowMin, out wasAboveMax);
        }

        /// <summary>
        /// Setting text via the <see cref="Text"/> property will update the value, this will only set the text
        /// </summary>
        private void SetTextOneWay ()
        {
            this.SetAndRaiseIfChanged(ref m_text, this.SourceValue.ToString(), nameof(Text));
            this.ApplyTextChange(updateSourceValue: false);
        }

        public bool TryParseString(string value, out dynamic castedValue)
        {
            return NumericConversion.TryParseString(value, this.ValueType, out castedValue);
        }

        internal void ForceType (Type newValue)
        {
            m_valueType = newValue;
            m_isTypeForced = true;
        }

        private void ApplyTextChange (bool updateSourceValue)
        {
            if (this.TryParseString(m_text, out dynamic sourceValue))
            {
                if (updateSourceValue)
                {
                    this.SetSourceValueOneWay(sourceValue);
                }

                // It's potentially a little bit of repeated work to do this cast (due to the SetSourceValueOneWay call above,
                // but it's the easiest to read & understand way to do this, so taking the small hit due to IsAtMinimum not
                // differentiating if it's too small, and same with maximum - and adding that seems wasteful since that can only
                // be done here.
                dynamic min = m_owner.Minimum == null ? NumericConversion.MinFor(this.ValueType) : NumericConversion.PerformSafeNumericCastToTarget(m_owner.Minimum, this.ValueType, out bool _, out bool _);
                dynamic max = m_owner.Maximum == null ? NumericConversion.MaxFor(this.ValueType) : NumericConversion.PerformSafeNumericCastToTarget(m_owner.Maximum, this.ValueType, out bool _, out bool _);
                if (sourceValue > max)
                {
                    this.TextErrorMessage = $"Value is invalid: beyond max ({m_owner.Maximum})";
                }
                else if (sourceValue < min)
                {
                    this.TextErrorMessage = $"Value is invalid: beyond min ({m_owner.Minimum})";
                }
                else
                {
                    this.TextErrorMessage = null;
                }
            }
            else
            {
                this.TextErrorMessage = $"Value is invalid: {this.ValueType.Name} could not be determined from input";
            }
        }

        /// <summary>
        /// Setting the value via the <see cref="SourceValue"/> property will update the text, this will only set the value
        /// </summary>
        private bool SetSourceValueOneWay (object value)
        {
            var cappedValue = this.RunValueCapping(value, out bool wasBelowMin, out bool wasAboveMax);
            this.IsAtMinimum = wasBelowMin;
            this.IsAtMaximum = wasAboveMax;

            if (this.SetAndRaiseIfChanged(ref m_sourceValue, cappedValue, nameof(SourceValue)))
            {
                this.ValueChanged?.Invoke(this, new EventArgs<object>(m_sourceValue));
                return true;
            }

            return false;
        }

        // ==================[ Static number boxing\unboxing helpers ]==================================
        internal delegate object NumericValueNudger (bool positive, object original, object nudge);
        internal delegate object NumericValueCapper (object value, object min, object max, out bool hadToCapAtMin, out bool hadToCapAtMax);
        internal delegate bool TryParser (string text, out object value);

        private static readonly Dictionary<Type, NumericValueNudger> kNudgers = new Dictionary<Type, NumericValueNudger>
        {
            { typeof(byte), RunNudge<byte> },
            { typeof(short), RunNudge<short> },
            { typeof(int), RunNudge<int> },
            { typeof(long), RunNudge<long> },
            { typeof(float), RunNudge<float> },
            { typeof(double), RunNudge<double> },
            { typeof(decimal), RunNudge<decimal> },
            { typeof(sbyte), RunNudge<sbyte> },
            { typeof(uint), RunNudge<uint> },
            { typeof(ushort), RunNudge<ushort> },
            { typeof(ulong), RunNudge<ulong> },
        };

        private static readonly Dictionary<Type, NumericValueCapper> kCappers = new Dictionary<Type, NumericValueCapper>
        {
            { typeof(byte), RunCapper<byte> },
            { typeof(short), RunCapper<short> },
            { typeof(int),  RunCapper<int> },
            { typeof(long),  RunCapper<long> },
            { typeof(float),  RunCapper<float> },
            { typeof(double),  RunCapper<double> },
            { typeof(decimal),  RunCapper<decimal> },
            { typeof(sbyte), RunCapper<sbyte> },
            { typeof(uint), RunCapper<uint> },
            { typeof(ushort), RunCapper<ushort> },
            { typeof(ulong), RunCapper<ulong> },
        };

        private static object RunCapper<T> (object value, object min, object max, out bool hadToCapAtMin, out bool hadToCapAtMax)
        {
            return NumericConversion.PerformNumericTypeSafeCapping<T>(value, min, max, out hadToCapAtMin, out hadToCapAtMax);
        }
        private static object RunNudge<T> (bool positive, object original, object nudge)
        {
            dynamic castedOriginal = NumericConversion.PerformSafeNumericCastToTarget(original, typeof(T), out _, out _);
            dynamic castedNudge = NumericConversion.PerformSafeNumericCastToTarget(nudge, typeof(T), out _, out _);
            return castedOriginal + (positive ? castedNudge : -castedNudge);
        }
    }
}
