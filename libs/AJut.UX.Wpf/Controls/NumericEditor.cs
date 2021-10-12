namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Input;
    using System.Windows.Media;
    using AJut;
    using DPUtils = DPUtils<NumericEditor>;

    // Todo: Make drag repeater, click and hold w/o moving starts spamming Triggered, or mouse move will capture while mouse is down and trigger Triggered on mouse move
    [TemplatePart(Name = nameof(PART_TextArea), Type = typeof(TextBox))]
    public class NumericEditor : Control
    {
        private TextBox PART_TextArea;
        private bool m_blockValueChangeReentrancy;

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
                this.NudgeIncrease();
            }

            void _OnNudgeDecreaseExecuted (object sender, RoutedEventArgs e)
            {
                this.NudgeDecrease();
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
                this.PART_TextArea.PreviewKeyDown -= _PreviewOnTextAreaKeyDown;
                this.PART_TextArea = null;
            }

            this.PART_TextArea = (TextBox)this.GetTemplateChild(nameof(PART_TextArea));
            this.PART_TextArea.PreviewKeyDown += _PreviewOnTextAreaKeyDown;

            void _PreviewOnTextAreaKeyDown (object _s, KeyEventArgs _e)
            {
                if (this.IsReadOnly)
                {
                    return;
                }

                if (_e.Key == Key.Return)
                {
                    var binding = BindingOperations.GetBindingExpression(this.PART_TextArea, TextBox.TextProperty);
                    if (binding != null)
                    {
                        binding.UpdateSource();
                        _e.Handled = true;
                    }
                }
                if (_e.Key == Key.Up)
                {
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                    {
                        this.NudgeBy(this.BigNudge);
                        _e.Handled = true;
                    }
                    else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        this.NudgeBy(this.SmallNudge);
                        _e.Handled = true;
                    }
                    else
                    {
                        this.NudgeBy(this.Nudge);
                        _e.Handled = true;
                    }
                }
                if (_e.Key == Key.Down)
                {
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                    {
                        this.NudgeBy(-this.BigNudge);
                        _e.Handled = true;
                    }
                    else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        this.NudgeBy(-this.SmallNudge);
                        _e.Handled = true;
                    }
                    else
                    {
                        this.NudgeBy(-this.Nudge);
                        _e.Handled = true;
                    }
                }
            }
        }

        // ===========================[ Commands ]================================================
        public static RoutedCommand NudgeIncreaseCommand = new RoutedCommand(nameof(NudgeIncrease), typeof(NumericEditor));
        public static RoutedCommand NudgeDecreaseCommand = new RoutedCommand(nameof(NudgeDecrease), typeof(NumericEditor));

        // ===========================[ Dependency Properties ]===================================
        public static readonly DependencyProperty ValueProperty = DPUtils.RegisterFP(_ => _.Value, 0.0f, (d, e) => d.OnValueChanged(e.NewValue), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault);
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

        public static readonly DependencyProperty DecimalPlacesAllowedProperty = DPUtils.Register(_ => _.DecimalPlacesAllowed, -1);
        public int DecimalPlacesAllowed
        {
            get => (int)this.GetValue(DecimalPlacesAllowedProperty);
            set => this.SetValue(DecimalPlacesAllowedProperty, value);
        }

        public static readonly DependencyProperty MinimumProperty = DPUtils.Register(_ => _.Minimum, 0.0, (d, e) => d.OnCapChanged());
        public double Minimum
        {
            get => (double)this.GetValue(MinimumProperty);
            set => this.SetValue(MinimumProperty, value);
        }

        public static readonly DependencyProperty MaximumProperty = DPUtils.Register(_ => _.Maximum, 1000.0, (d, e) => d.OnCapChanged());
        public double Maximum
        {
            get => (double)this.GetValue(MaximumProperty);
            set => this.SetValue(MaximumProperty, value);
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

        // ===========================[ Interface Methods ]===================================
        /// <summary>
        /// Get the value in casted storage
        /// </summary>
        public T GetValue<T> () => (T)Convert.ChangeType(this.Value, typeof(T));

        /// <summary>
        /// Increase the value by the <see cref="Nudge"/> value
        /// </summary>
        public void NudgeIncrease ()
        {
            this.NudgeBy(this.Nudge);
        }

        /// <summary>
        /// Decrease the value by the <see cref="Nudge"/> value
        /// </summary>
        public void NudgeDecrease ()
        {
            this.NudgeBy(-this.Nudge);
        }

        /// <summary>
        /// Nudge by a passed in value (which will be converted to the target type before application)
        /// </summary>
        /// <param name="value">The value to nudge by, will be converted to the target type before application</param>
        public void NudgeBy (dynamic value)
        {
            // Preserve the caret location
            int caretOffset = this.DisplayValue.Text.Length - this.PART_TextArea.CaretIndex;
            int selectionLength = this.PART_TextArea.SelectionLength;
            bool isAllSelected = selectionLength == this.DisplayValue.Text.Length;

            // Perform the nudge
            this.DisplayValue.Nudge(true, Convert.ChangeType(value, this.DisplayValue.ValueType));

            // Preserve the caret location
            caretOffset = AJut.MathUtilities.Cap.Within(0, this.DisplayValue.Text.Length, this.DisplayValue.Text.Length - caretOffset);
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
                this.PART_TextArea.CaretIndex = caretOffset;
            }
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
                switch (newValue)
                {
                    case float v:
                        _ForceCapMinMax(float.MinValue, float.MaxValue);
                        this.DisplayValue = new TextEditNumberViewModel(this, v);
                        break;

                    case double v:
                        _ForceCapMinMax(double.MinValue, double.MaxValue);
                        this.DisplayValue = new TextEditNumberViewModel(this, v);
                        break;

                    case decimal v:
                        _ForceCapMinMax(decimal.MinValue, decimal.MaxValue);
                        this.DisplayValue = new TextEditNumberViewModel(this, v);
                        break;

                    case byte v:
                        _ForceCapMinMax(byte.MinValue, byte.MaxValue);
                        this.DisplayValue = new TextEditNumberViewModel(this, v);
                        break;

                    case short v:
                        _ForceCapMinMax(short.MinValue, short.MaxValue);
                        this.DisplayValue = new TextEditNumberViewModel(this, v);
                        break;

                    case int v:
                        _ForceCapMinMax(int.MinValue, int.MaxValue);
                        this.DisplayValue = new TextEditNumberViewModel(this, v);
                        break;

                    case long v:
                        _ForceCapMinMax(long.MinValue, long.MaxValue);
                        this.DisplayValue = new TextEditNumberViewModel(this, v);
                        break;

                    case GridLength v:
                        _ForceCapMinMax(double.MinValue, double.MaxValue);
                        this.DisplayValue = new TextEditNumberViewModel(this, v.Value);
                        break;
                }
            }
            finally
            {
                m_blockValueChangeReentrancy = false;
            }

            void _ForceCapMinMax<T> (T _typedMin, T _typedMax)
            {
                var _doubleMin = (double)Convert.ChangeType(_typedMin, typeof(double));
                if (this.Minimum < _doubleMin)
                {
                    this.Minimum = _doubleMin;
                }

                var _doubleMax = (double)Convert.ChangeType(_typedMax, typeof(double));
                if (this.Maximum > _doubleMax)
                {
                    this.Maximum = _doubleMax;
                }
            }
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

        // =====================[ Construction ]==========================
        public TextEditNumberViewModel (NumericEditor owner, object value)
        {
            m_owner = owner;
            if (value == null)
            {
                value = 0.0f;
            }

            this.ValueType = value.GetType();
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
                if (this.SetAndRaiseIfChanged(ref m_text, value) && kParsers[this.ValueType](m_text, out object sourceValue))
                {
                    this.SetSourceValueOneWay(sourceValue);
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

        public Type ValueType { get; private set; }

        // =====================[ Methods ]=============================

        public void Nudge (bool positive, object nudgeValue)
        {
            this.SourceValue = kNudgers[this.ValueType].Invoke(positive, this.SourceValue, nudgeValue);
        }

        internal void ReevaluateCap ()
        {
            this.SourceValue = this.SourceValue;
        }

        private object CapNewValue (object value, out bool wasBelowMin, out bool wasAboveMax)
        {
            return kCappers[this.ValueType](value, m_owner.Minimum, m_owner.Maximum, out wasBelowMin, out wasAboveMax);
        }

        /// <summary>
        /// Setting text via the <see cref="Text"/> property will update the value, this will only set the text
        /// </summary>
        private void SetTextOneWay ()
        {
            this.SetAndRaiseIfChanged(ref m_text, this.SourceValue.ToString(), nameof(Text));
        }

        /// <summary>
        /// Setting the value via the <see cref="SourceValue"/> property will update the text, this will only set the value
        /// </summary>
        private bool SetSourceValueOneWay (object value)
        {
            var cappedValue = this.CapNewValue(value, out bool wasBelowMin, out bool wasAboveMax);
            this.IsAtMinimum = wasBelowMin;
            this.IsAtMaximum = wasAboveMax;

            if (this.SetAndRaiseIfChanged(ref m_sourceValue, cappedValue, nameof(SourceValue)))
            {
                this.ValueType = this.SourceValue.GetType();
                this.ValueChanged?.Invoke(this, new EventArgs<object>(m_sourceValue));
                return true;
            }

            return false;
        }

        // ==================[ Static number boxing\unboxing helpers ]==================================
        private delegate object Nudger (bool positive, object original, object nudge);
        private delegate object Capper (object newValue, object min, object max, out bool cappedInMin, out bool cappedInMax);
        private delegate bool TryParser (string text, out object value);
        private delegate bool TypedTryParser<T> (string text, out T value);

        private static readonly Dictionary<Type, TryParser> kParsers = new Dictionary<Type, TryParser>
        {
            { typeof(byte), DownCastTryParse<byte>(byte.TryParse) },
            { typeof(short), DownCastTryParse<short>(short.TryParse) },
            { typeof(int), DownCastTryParse<int>(int.TryParse) },
            { typeof(long), DownCastTryParse<long>(long.TryParse) },
            { typeof(float), DownCastTryParse<float>(float.TryParse) },
            { typeof(double), DownCastTryParse<double>(double.TryParse) },
            { typeof(decimal), DownCastTryParse<decimal>(decimal.TryParse) },
            { typeof(sbyte), DownCastTryParse<sbyte>(sbyte.TryParse) },
            { typeof(uint), DownCastTryParse<uint>(uint.TryParse) },
            { typeof(ushort), DownCastTryParse<ushort>(ushort.TryParse) },
            { typeof(ulong), DownCastTryParse<ulong>(ulong.TryParse) },
        };

        private static readonly Dictionary<Type, Nudger> kNudgers = new Dictionary<Type, Nudger>
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
        private static readonly Dictionary<Type, Capper> kCappers = new Dictionary<Type, Capper>
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

        private static object RunCapper<T> (object value, object min, object max, out bool cappedMin, out bool cappedMax)
        {
            cappedMin = false;
            cappedMax = false;
            dynamic newValue = Convert.ChangeType(value, typeof(T));

            dynamic castedMin = Convert.ChangeType(min, typeof(T));
            dynamic castedMax = Convert.ChangeType(max, typeof(T));
            if (newValue <= castedMin)
            {
                cappedMin = true;
                return castedMin;
            }

            if (newValue >= castedMax)
            {
                cappedMax = true;
                return castedMax;
            }

            return value;
        }

        private static object RunNudge<T> (bool positive, object original, object nudge)
        {
            dynamic castedOriginal = Convert.ChangeType(original, typeof(T));
            dynamic castedNudge = Convert.ChangeType(nudge, typeof(T));
            return Convert.ChangeType(castedOriginal + (positive ? castedNudge : -castedNudge), typeof(T));
        }

        private static TryParser DownCastTryParse<T> (TypedTryParser<T> typedParser)
        {
            return
                (string t, out object o) =>
                {
                    if (typedParser(t, out T value))
                    {
                        o = value;
                        return true;
                    }

                    o = null;
                    return false;
                };
        }
    }
}
