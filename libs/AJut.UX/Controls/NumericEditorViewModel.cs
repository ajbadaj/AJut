namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using AJut;
    using AJut.Storage;
    using AJut.TypeManagement;

    // ===========[ INumericEditorSettings ]=====================================
    // Contract between NumericEditorViewModel and the host control.
    // Both the WPF NumericEditor and the WinUI3 NumericEditor implement this
    // so the shared view model has no dependency on either UI framework.

    public interface INumericEditorSettings
    {
        /// <summary>Number of decimal places allowed. -1 = unconstrained.</summary>
        int DecimalPlacesAllowed { get; }

        /// <summary>Minimum bound as a boxed numeric, or null for unconstrained.</summary>
        object Minimum { get; }

        /// <summary>Maximum bound as a boxed numeric, or null for unconstrained.</summary>
        object Maximum { get; }

        /// <summary>How the editor should respond when entered text is outside the min/max bounds.</summary>
        eOutOfBoundsResponse OutOfBoundsResponse { get; }
    }

    // ===========[ NumericEditorViewModel ]=====================================
    // Platform-agnostic view model for a numeric editor control. Handles:
    //  • type detection from the initial value
    //  • text <--> value round-trip (used by WPF NumericEditor TextBox)
    //  • min/max capping (type-safe via NumericConversion)
    //  • nudge logic for increment/decrement actions
    //  • decimal-places enforcement
    //
    // Extracted from WPF TextEditNumberViewModel so WinUI3 NumericEditor can share
    // the same type-detection and round-trip logic without duplicating it.

    public class NumericEditorViewModel : NotifyPropertyChanged
    {
        private readonly INumericEditorSettings m_settings;
        private bool m_isTypeForced;
        private Type m_valueType;

        // =====================[ Construction ]==========================
        public NumericEditorViewModel (INumericEditorSettings settings, object value, Type enforcedType = null)
        {
            m_settings = settings;

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
                value = enforcedType != null
                    ? Activator.CreateInstance(enforcedType)
                    : 0.0f;
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

        // =====================[ Properties ]============================
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
            set => this.SetAndRaiseIfChanged(ref m_textErrorMessage, value, nameof(TextErrorMessage), nameof(IsTextInErrorState), nameof(ShouldShowError));
        }

        public bool IsTextInErrorState => m_textErrorMessage != null;

        /// <summary>
        /// Whether the error visuals (border, glyph, tooltip) should be shown - true only when the text
        /// is out of bounds AND the configured response is <see cref="eOutOfBoundsResponse.ErrorAndToolTip"/>.
        /// </summary>
        public bool ShouldShowError => this.IsTextInErrorState && m_settings.OutOfBoundsResponse == eOutOfBoundsResponse.ErrorAndToolTip;

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

        // =====================[ Methods ]==============================

        public bool TryCapTextToMaxDecimalPlaces ()
        {
            Regex digitManagedParse;
            int decimalPlacesAllowed = m_settings.DecimalPlacesAllowed;

            if (decimalPlacesAllowed < 0)
            {
                digitManagedParse = new Regex(@"(-?\d+\.\d+)?(-?\d+)?");
            }
            else if (decimalPlacesAllowed == 0)
            {
                digitManagedParse = new Regex(@"(-?\d+)");
            }
            else
            {
                digitManagedParse = new Regex($@"(-?\d+\.?\d{{0,{decimalPlacesAllowed}}})");
            }

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

        public void ReevaluateCap ()
        {
            this.SourceValue = this.SourceValue;
        }

        /// <summary>
        /// Pushes the committed (already capped) source value back into the display text so an out of
        /// range entry - e.g. typing 5500 against a max of 10 - snaps to the clamped value instead of
        /// lingering as text that no longer matches the real value.
        /// </summary>
        public void ResyncTextToSourceValue ()
        {
            this.SetTextOneWay();
        }

        /// <summary>
        /// Finalizes an edit session (lost focus, Enter). When the response is
        /// <see cref="eOutOfBoundsResponse.FixOnCommit"/> the displayed text is snapped to the clamped
        /// value; otherwise the out of bounds text is left in place to keep flagging the error.
        /// </summary>
        public void CommitEdit ()
        {
            if (m_settings.OutOfBoundsResponse == eOutOfBoundsResponse.FixOnCommit)
            {
                this.ResyncTextToSourceValue();
            }
        }

        /// <summary>
        /// Re-raises <see cref="ShouldShowError"/> - call when the out of bounds response changes so
        /// bound error visuals re-evaluate without the text itself changing.
        /// </summary>
        public void RefreshErrorDisplay ()
        {
            this.RaisePropertyChanged(nameof(ShouldShowError));
        }

        public void ForceType (Type newValue)
        {
            m_valueType = newValue;
            m_isTypeForced = true;
        }

        public bool TryParseString (string value, out dynamic castedValue)
        {
            return NumericConversion.TryParseString(value, this.ValueType, out castedValue);
        }

        // =====================[ Private Helpers ]======================

        private object RunValueCapping (object value, out bool wasBelowMin, out bool wasAboveMax)
        {
            return kCappers[this.ValueType](value, m_settings.Minimum, m_settings.Maximum, out wasBelowMin, out wasAboveMax);
        }

        /// <summary>Set Text without triggering a source-value update.</summary>
        private void SetTextOneWay ()
        {
            this.SetAndRaiseIfChanged(ref m_text, this.SourceValue.ToString(), nameof(Text));
            this.ApplyTextChange(updateSourceValue: false);
        }

        private void ApplyTextChange (bool updateSourceValue)
        {
            if (this.TryParseString(m_text, out dynamic sourceValue))
            {
                if (updateSourceValue)
                {
                    this.SetSourceValueOneWay(sourceValue);
                }

                dynamic min = m_settings.Minimum == null
                    ? NumericConversion.MinFor(this.ValueType)
                    : NumericConversion.PerformSafeNumericCastToTarget(m_settings.Minimum, this.ValueType, out bool _, out bool _);
                dynamic max = m_settings.Maximum == null
                    ? NumericConversion.MaxFor(this.ValueType)
                    : NumericConversion.PerformSafeNumericCastToTarget(m_settings.Maximum, this.ValueType, out bool _, out bool _);

                if (sourceValue > max)
                {
                    this.TextErrorMessage = $"Value exceeds the max and was capped to {m_settings.Maximum}";
                }
                else if (sourceValue < min)
                {
                    this.TextErrorMessage = $"Value is below the min and was capped to {m_settings.Minimum}";
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

        /// <summary>Set SourceValue without triggering a text update.</summary>
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

        // =====================[ Static nudge / cap tables ]============
        internal delegate object NumericValueNudger (bool positive, object original, object nudge);
        internal delegate object NumericValueCapper (object value, object min, object max, out bool hadToCapAtMin, out bool hadToCapAtMax);

        private static readonly Dictionary<Type, NumericValueNudger> kNudgers = new Dictionary<Type, NumericValueNudger>
        {
            { typeof(byte),    RunNudge<byte> },
            { typeof(short),   RunNudge<short> },
            { typeof(int),     RunNudge<int> },
            { typeof(long),    RunNudge<long> },
            { typeof(float),   RunNudge<float> },
            { typeof(double),  RunNudge<double> },
            { typeof(decimal), RunNudge<decimal> },
            { typeof(sbyte),   RunNudge<sbyte> },
            { typeof(uint),    RunNudge<uint> },
            { typeof(ushort),  RunNudge<ushort> },
            { typeof(ulong),   RunNudge<ulong> },
        };

        private static readonly Dictionary<Type, NumericValueCapper> kCappers = new Dictionary<Type, NumericValueCapper>
        {
            { typeof(byte),    RunCapper<byte> },
            { typeof(short),   RunCapper<short> },
            { typeof(int),     RunCapper<int> },
            { typeof(long),    RunCapper<long> },
            { typeof(float),   RunCapper<float> },
            { typeof(double),  RunCapper<double> },
            { typeof(decimal), RunCapper<decimal> },
            { typeof(sbyte),   RunCapper<sbyte> },
            { typeof(uint),    RunCapper<uint> },
            { typeof(ushort),  RunCapper<ushort> },
            { typeof(ulong),   RunCapper<ulong> },
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
