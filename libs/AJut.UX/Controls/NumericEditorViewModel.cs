namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Numerics;
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
    //  - type detection from the initial value
    //  - text <--> value round-trip (used by WPF NumericEditor TextBox)
    //  - min/max capping (type-safe via NumericConversion)
    //  - nudge logic for increment/decrement actions
    //  - decimal-places enforcement
    //
    // Extracted from WPF TextEditNumberViewModel so WinUI3 NumericEditor can share
    // the same type-detection and round-trip logic without duplicating it.

    public class NumericEditorViewModel : NotifyPropertyChanged
    {
        private readonly INumericEditorSettings m_settings;
        private bool m_isTypeForced;
        private Type m_valueType;

        // =====================[ Construction ]==========================
        public NumericEditorViewModel (INumericEditorSettings settings, object value, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type enforcedType = null)
        {
            m_settings = settings;

            if (enforcedType != null)
            {
                m_isTypeForced = true;
                m_valueType = enforcedType;
            }

            if (value is string stringValue)
            {
                if (NumericConversion.TryParseString(stringValue, enforcedType ?? typeof(float), out object castedValue))
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

            // A nudge steps from the real value, so it also replaces text that was refused, even when the value lands
            // where it was (a nudge up at the maximum)
            this.SetTextOneWay();
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
        /// value; otherwise the out of bounds text is left in place to keep flagging the error. Text that
        /// is not a number of this editor's type at all (a fraction on an integer type, letters) never
        /// became the value, so it is always put back to the real value.
        /// </summary>
        public void CommitEdit ()
        {
            if ((m_settings.OutOfBoundsResponse == eOutOfBoundsResponse.FixOnCommit)
                || !this.TryParseString(m_text, out object _))
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

        /// <summary>Parses <paramref name="value"/> as this editor's <see cref="ValueType"/>, boxed in that type.</summary>
        public bool TryParseString (string value, out object castedValue)
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
            if (this.TryParseString(m_text, out object sourceValue))
            {
                if (updateSourceValue)
                {
                    this.SetSourceValueOneWay(sourceValue);
                }

                object min = m_settings.Minimum == null
                    ? NumericConversion.MinFor(this.ValueType)
                    : NumericConversion.PerformSafeNumericCastToTarget(m_settings.Minimum, this.ValueType, out bool _, out bool _);
                object max = m_settings.Maximum == null
                    ? NumericConversion.MaxFor(this.ValueType)
                    : NumericConversion.PerformSafeNumericCastToTarget(m_settings.Maximum, this.ValueType, out bool _, out bool _);

                NumericValueComparer compare = kComparers[this.ValueType];
                if (compare(sourceValue, max) > 0)
                {
                    this.TextErrorMessage = $"Value exceeds the max and was capped to {m_settings.Maximum}";
                }
                else if (compare(sourceValue, min) < 0)
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
        internal delegate int NumericValueComparer (object first, object second);

        // Each nudge adds in the type C#'s own arithmetic would use for the value's type (see RunNudge), so the capper
        // sees exactly what it saw when this arithmetic was done with dynamic: the small integer types add as int, as
        // C# promotes them, and a uint stepping down goes through long, as unary minus on a uint does. The one change
        // is ulong stepping down, which dynamic could not do at all (there is no unary minus on ulong); it goes
        // through decimal, which holds the whole ulong range, and the capper stops it at zero.
        private static readonly Dictionary<Type, NumericValueNudger> kNudgers = new Dictionary<Type, NumericValueNudger>
        {
            { typeof(byte),    RunNudge<byte, int, int> },
            { typeof(short),   RunNudge<short, int, int> },
            { typeof(int),     RunNudge<int, int, int> },
            { typeof(long),    RunNudge<long, long, long> },
            { typeof(float),   RunNudge<float, float, float> },
            { typeof(double),  RunNudge<double, double, double> },
            { typeof(decimal), RunNudge<decimal, decimal, decimal> },
            { typeof(sbyte),   RunNudge<sbyte, int, int> },
            { typeof(uint),    RunNudge<uint, uint, long> },
            { typeof(ushort),  RunNudge<ushort, int, int> },
            { typeof(ulong),   RunNudge<ulong, ulong, decimal> },
        };

        // Compares two values boxed in the same numeric type, in that type's own arithmetic (a NaN is neither above
        // nor below anything, as with C#'s comparison operators)
        private static readonly Dictionary<Type, NumericValueComparer> kComparers = new Dictionary<Type, NumericValueComparer>
        {
            { typeof(byte),    CompareIn<byte> },
            { typeof(short),   CompareIn<short> },
            { typeof(int),     CompareIn<int> },
            { typeof(long),    CompareIn<long> },
            { typeof(float),   CompareIn<float> },
            { typeof(double),  CompareIn<double> },
            { typeof(decimal), CompareIn<decimal> },
            { typeof(sbyte),   CompareIn<sbyte> },
            { typeof(uint),    CompareIn<uint> },
            { typeof(ushort),  CompareIn<ushort> },
            { typeof(ulong),   CompareIn<ulong> },
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
            where T : INumber<T>
        {
            return NumericConversion.PerformNumericTypeSafeCapping<T>(value, min, max, out hadToCapAtMin, out hadToCapAtMax);
        }

        // The value and the step are both cast to T first, then added (or the step subtracted) in TUp for a step up and
        // TDown for a step down, and the result is handed to the capper as it comes out, which brings it back into T.
        private static object RunNudge<T, TUp, TDown> (bool positive, object original, object nudge)
            where T : INumber<T>
            where TUp : INumber<TUp>
            where TDown : INumber<TDown>
        {
            T castedOriginal = (T)NumericConversion.PerformSafeNumericCastToTarget(original, typeof(T), out _, out _);
            T castedNudge = (T)NumericConversion.PerformSafeNumericCastToTarget(ToWholeStepIfIntegral<T>(nudge), typeof(T), out _, out _);
            if (positive)
            {
                return TUp.CreateTruncating(castedOriginal) + TUp.CreateTruncating(castedNudge);
            }

            return TDown.CreateTruncating(castedOriginal) - TDown.CreateTruncating(castedNudge);
        }

        private static int CompareIn<T> (object first, object second) where T : INumber<T>
        {
            T a = (T)first;
            T b = (T)second;
            if (a < b)
            {
                return -1;
            }

            return a > b ? 1 : 0;
        }

        /// <summary>
        /// For an integer type, turns a fractional step into a whole one: rounded away from zero, and never smaller
        /// than 1 (a zero step stays zero). A floating point step is returned as is.
        /// </summary>
        private static object ToWholeStepIfIntegral<T> (object nudge)
        {
            // The cast to T below goes through Convert.ChangeType, and converting a double to an integer type that way
            // rounds half to even (banker's rounding): 0.5 becomes 0, 1.5 becomes 2, 2.5 becomes 2. See
            // https://learn.microsoft.com/dotnet/api/system.convert.toint32#system-convert-toint32(system-double).
            // That made the default SmallNudge of 0.5 a step of nothing on any integer editor, so a Shift-nudge did
            // not move the value at all.
            //
            // An integer cannot move by less than 1, and a nudge that does nothing reads as broken, so a fractional
            // step on an integer type rounds away from zero (0.5 steps by 1, 2.5 by 3, the way anybody typing the
            // number expects) and is held to at least 1. Floating point types keep the step exactly as given.
            Type type = typeof(T);
            if ((type == typeof(float))
                || (type == typeof(double))
                || (type == typeof(decimal)))
            {
                return nudge;
            }

            double step = Convert.ToDouble(nudge);
            if (step == 0.0)
            {
                return step;
            }

            double wholeMagnitude = Math.Max(1.0, Math.Round(Math.Abs(step), MidpointRounding.AwayFromZero));
            return Math.Sign(step) * wholeMagnitude;
        }
    }
}
