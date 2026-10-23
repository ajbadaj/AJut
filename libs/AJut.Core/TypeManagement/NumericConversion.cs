namespace AJut.TypeManagement
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Text.RegularExpressions;

    /// <summary>
    /// It's not always so straightforward to convert numbers between numeric types. These utilities help to ensure this is done safely.
    /// </summary>
    /// <remarks>
    /// Values whose numeric type is only known at runtime travel as boxed objects and are converted with
    /// <see cref="Convert"/>, never through C# dynamic: dynamic needs the runtime binder, which compiles code at runtime,
    /// and an ahead of time compiled app (iOS, and any NativeAOT build) has no compiler at runtime, so dynamic throws
    /// there. Where a comparison has to happen in the value's own type, it goes through typed generic math
    /// (<see cref="INumber{TSelf}"/>), which compiles ahead of time like any other generic code.
    /// </remarks>
    public static class NumericConversion
    {
        // ===================================
        // Delegate Types
        // ===================================
        private delegate bool BoxedStringParser (string text, out object value);
        private delegate bool TypedNumericStringParser<T> (string text, out T value);

        // ===================================
        // Private Helper Fields
        // ===================================
        private static readonly Regex kNumericWithFloatPointParser = new Regex(@"(?:-?\d*\.\d+)|(?:-?\d+)");
        private static readonly Regex kNumericUnsignedIntegerPointParser = new Regex(@"\d+");
        private static readonly Regex kNumericSignedIntegerPointParser = new Regex(@"\-?\d+");

        private static readonly Dictionary<Type, NumericBounds> kMinMaxes = new Dictionary<Type, NumericBounds>
        {
            { typeof(byte), new NumericBounds(byte.MinValue, byte.MaxValue) },
            { typeof(short), new NumericBounds(short.MinValue, short.MaxValue) },
            { typeof(int), new NumericBounds(int.MinValue, int.MaxValue) },
            { typeof(long), new NumericBounds(long.MinValue, long.MaxValue) },
            { typeof(float), new NumericBounds(float.MinValue, float.MaxValue) },
            { typeof(double), new NumericBounds(double.MinValue, double.MaxValue) },
            { typeof(decimal), new NumericBounds(decimal.MinValue, decimal.MaxValue) },
            { typeof(sbyte), new NumericBounds(sbyte.MinValue, sbyte.MaxValue) },
            { typeof(uint), new NumericBounds(uint.MinValue, uint.MaxValue) },
            { typeof(ushort), new NumericBounds(ushort.MinValue, ushort.MaxValue) },
            { typeof(ulong), new NumericBounds(ulong.MinValue, ulong.MaxValue) },
        };

        private static readonly Dictionary<Type, bool> kIsFloatingPointBasedNumericType = new Dictionary<Type, bool>
        {
            { typeof(byte), false },
            { typeof(short), false },
            { typeof(int), false },
            { typeof(long), false },
            { typeof(float), true },
            { typeof(double), true },
            { typeof(decimal), true },
            { typeof(sbyte), false },
            { typeof(uint), false },
            { typeof(ushort), false },
            { typeof(ulong), false },
        };

        private static readonly Dictionary<Type, bool> kIsUnsignedNumericType = new Dictionary<Type, bool>
        {
            { typeof(byte), true },
            { typeof(short), false },
            { typeof(int), false },
            { typeof(long), false },
            { typeof(float), false },
            { typeof(double), false },
            { typeof(decimal), false },
            { typeof(sbyte), false },
            { typeof(uint), true },
            { typeof(ushort), true },
            { typeof(ulong), true },
        };

        private static readonly Dictionary<Type, BoxedStringParser> kParsers = new Dictionary<Type, BoxedStringParser>
        {
            { typeof(byte), BuildTypedNumericStringParser<byte>(byte.TryParse) },
            { typeof(short), BuildTypedNumericStringParser<short>(short.TryParse) },
            { typeof(int), BuildTypedNumericStringParser<int>(int.TryParse) },
            { typeof(long), BuildTypedNumericStringParser<long>(long.TryParse) },
            { typeof(float), BuildTypedNumericStringParser<float>(float.TryParse) },
            { typeof(double), BuildTypedNumericStringParser<double>(double.TryParse) },
            { typeof(decimal), BuildTypedNumericStringParser<decimal>(decimal.TryParse) },
            { typeof(sbyte), BuildTypedNumericStringParser<sbyte>(sbyte.TryParse) },
            { typeof(uint), BuildTypedNumericStringParser<uint>(uint.TryParse) },
            { typeof(ushort), BuildTypedNumericStringParser<ushort>(ushort.TryParse) },
            { typeof(ulong), BuildTypedNumericStringParser<ulong>(ulong.TryParse) },
        };

        // ===================================
        // Public interface
        // ===================================
        public static bool IsSupportedNumericType (Type type) => kParsers.Keys.Contains(type);

        public static T MinFor<T> () => (T)kMinMaxes[typeof(T)].Min;

        /// <summary>The smallest value of <paramref name="targetNumericType"/>, boxed in that type.</summary>
        public static object MinFor (Type targetNumericType) => kMinMaxes[targetNumericType].Min;

        public static T MaxFor<T> () => (T)kMinMaxes[typeof(T)].Max;

        /// <summary>The largest value of <paramref name="targetNumericType"/>, boxed in that type.</summary>
        public static object MaxFor (Type targetNumericType) => kMinMaxes[targetNumericType].Max;

        /// <summary>
        /// Converts a boxed number to <paramref name="targetType"/>, capping it to that type's range rather than letting it
        /// overflow, and returns it boxed in <paramref name="targetType"/>. The flags say whether it had to be capped.
        /// </summary>
        public static object PerformSafeNumericCastToTarget (object value, Type targetType, out bool didCapToNumericBoundaryMin, out bool didCapToNumericBoundaryMax)
        {
            didCapToNumericBoundaryMin = false;
            didCapToNumericBoundaryMax = false;

            // If the value is already of the target type, just return it
            if (value.GetType() == targetType)
            {
                return value;
            }

            // Otherwise the value is compared against the target's bounds in a type that holds the value, the minimum and the
            //  maximum without overflowing, and either capped to a bound or converted. Converting first is not safe: an int of
            //  5,000,000 converted to a byte overflows before there is anything to cap.

            NumericBounds minMaxOfTarget = kMinMaxes[targetType];

            // If we're a floating point based target
            if (kIsFloatingPointBasedNumericType[targetType])
            {
                // .. then double holds the largest minimum and maximum values
                double valueAsDouble = Convert.ToDouble(value);
                double targetMinAsDouble = Convert.ToDouble(minMaxOfTarget.Min);
                double targetMaxAsDouble = Convert.ToDouble(minMaxOfTarget.Max);

                if (valueAsDouble < targetMinAsDouble)
                {
                    valueAsDouble = targetMinAsDouble;
                    didCapToNumericBoundaryMin = true;
                }
                else if (valueAsDouble > targetMaxAsDouble)
                {
                    valueAsDouble = targetMaxAsDouble;
                    didCapToNumericBoundaryMax = true;
                }

                return Convert.ChangeType(valueAsDouble, targetType);
            }

            // An integer target. Any negative value caps an unsigned target at zero, even one that would round to zero.
            if (kIsUnsignedNumericType[targetType] && IsNegative(value))
            {
                didCapToNumericBoundaryMin = true;
                return minMaxOfTarget.Min;
            }

            // The value is rounded half to even first, which is how Convert rounds, so a value that rounds onto a bound
            //  converts rather than caps. A float or double is compared as a double, since it can be far beyond what decimal
            //  holds, and anything else as a decimal, which holds every integer type exactly.
            bool isBelowMin;
            bool isAboveMax;
            if (value is double || value is float)
            {
                double rounded = Math.Round(Convert.ToDouble(value));

                // Every integer type's minimum is exact as a double. long's and ulong's maximums are not: they round up to
                //  the next power of two, which is itself out of range. One past the maximum rounds to that same power of
                //  two, and is exact for every smaller type, so the test is whether the value reaches one past the maximum.
                isBelowMin = rounded < Convert.ToDouble(minMaxOfTarget.Min);
                isAboveMax = rounded >= Convert.ToDouble(minMaxOfTarget.Max) + 1.0;
            }
            else
            {
                decimal rounded = Math.Round(Convert.ToDecimal(value));
                isBelowMin = rounded < Convert.ToDecimal(minMaxOfTarget.Min);
                isAboveMax = rounded > Convert.ToDecimal(minMaxOfTarget.Max);
            }

            if (isBelowMin)
            {
                didCapToNumericBoundaryMin = true;
                return minMaxOfTarget.Min;
            }

            if (isAboveMax)
            {
                didCapToNumericBoundaryMax = true;
                return minMaxOfTarget.Max;
            }

            return Convert.ChangeType(value, targetType);
        }

        /// <summary>
        /// Converts a boxed number to <typeparamref name="T"/> (capped to its range, see
        /// <see cref="PerformSafeNumericCastToTarget"/>), then caps it to <paramref name="min"/> and <paramref name="max"/>,
        /// which may be boxed in any numeric type, or null for the type's own bound.
        /// </summary>
        public static T PerformNumericTypeSafeCapping<T> (object value, object min, object max, out bool hadToCapAtMin, out bool hadToCapAtMax)
            where T : INumber<T>
        {
            // First cap value to be within the numeric bounds
            Type targetType = typeof(T);

            // This will ensure value is expressed in terms of the numeric boundaries (ie an int of 5,000,00 will be cast to a byte of 255)
            T castedValue = (T)PerformSafeNumericCastToTarget(value, targetType, out hadToCapAtMin, out hadToCapAtMax);

            NumericBounds targetTypedMinMax = kMinMaxes[targetType];
            T castedMin = (T)(min == null ? targetTypedMinMax.Min : PerformSafeNumericCastToTarget(min, targetType, out bool _, out bool _));
            T castedMax = (T)(max == null ? targetTypedMinMax.Max : PerformSafeNumericCastToTarget(max, targetType, out bool _, out bool _));

            // Next to ensure our value is within the user set boundaries
            if (castedValue < castedMin)
            {
                castedValue = castedMin;
                hadToCapAtMin = true;
            }
            else if (castedValue > castedMax)
            {
                castedValue = castedMax;
                hadToCapAtMax = true;
            }

            return castedValue;
        }

        public static bool TryParseString<T> (string stringValue, out T typedNumericValue)
        {
            if (kParsers[typeof(T)](stringValue, out object value))
            {
                typedNumericValue = (T)value;
                return true;
            }

            typedNumericValue = default;
            return false;
        }

        /// <summary>
        /// Parses <paramref name="stringValue"/> as <paramref name="numericType"/>, boxed in that type. When the type's own
        /// parse fails, the first run of digits in the text that suits the type is parsed instead, except that an integer
        /// type refuses text whose first number has a fraction ("43.5" is not an int).
        /// </summary>
        public static bool TryParseString (string stringValue, Type numericType, out object numericValue)
        {
            if (kParsers[numericType](stringValue, out object value))
            {
                numericValue = value;
                return true;
            }

            numericValue = default;
            return false;
        }

        // ===================================
        // Helpers
        // ===================================
        // Whether a boxed number is below zero, read in its own type. Every supported type converts to double exactly
        // enough for a sign test, and a NaN reads as not negative, as a comparison in its own type would.
        private static bool IsNegative (object value) => Convert.ToDouble(value) < 0;

        private static BoxedStringParser BuildTypedNumericStringParser<T> (TypedNumericStringParser<T> typedParser)
        {
            Type targetType = typeof(T);
            return (string stringValue, out object parsedOutputValue) =>
            {
                // First try the normal string parser
                if (typedParser(stringValue, out T parsedValueTyped))
                {
                    parsedOutputValue = parsedValueTyped;
                    return true;
                }

                // If that doesn't work, then search the string for appropriate digits and try again
                if (_TryGetSubstringFor(stringValue, out string foundSubstring)
                    && typedParser(foundSubstring, out parsedValueTyped))
                {
                    parsedOutputValue = parsedValueTyped;
                    return true;
                }

                parsedOutputValue = null;
                return false;
            };

            bool _TryGetSubstringFor (string _toSearch, out string _subString)
            {
                // An integer type cannot hold a number with a fraction, so text whose first number has one ("43.5") is
                // refused rather than read as its whole part, which would hand back a different number than the text
                // says with nothing to show for it
                bool isFloatingPoint = kIsFloatingPointBasedNumericType[targetType];
                if (!isFloatingPoint)
                {
                    Match firstNumber = kNumericWithFloatPointParser.Match(_toSearch);
                    if (firstNumber.Success && firstNumber.Value.Contains('.'))
                    {
                        _subString = null;
                        return false;
                    }
                }

                Regex toUse;
                if (isFloatingPoint)
                {
                    toUse = kNumericWithFloatPointParser;
                }
                else if (kIsUnsignedNumericType[targetType])
                {
                    toUse = kNumericUnsignedIntegerPointParser;
                }
                else
                {
                    toUse = kNumericSignedIntegerPointParser;
                }

                Match match = toUse.Match(_toSearch);
                if (match.Success)
                {
                    _subString = _toSearch.Substring(match.Index, match.Length);
                    return true;
                }

                _subString = null;
                return false;
            }
        }

        // ===========[ Subclasses/structs ]===================================
        // A numeric type's own minimum and maximum, each boxed in that type
        private sealed record NumericBounds (object Min, object Max);
    }
}
