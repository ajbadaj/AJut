namespace AJut.TypeManagement
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Permissions;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    /// <summary>
    /// It's not always so straightforward to convert numbers between numeric types. These utilities help to ensure this is done safely.
    /// </summary>
    public static class NumericConversion
    {
        // ===================================
        // Delegate Types
        // ===================================
        private delegate bool AdvancedStringParser (string text, out dynamic value);
        private delegate bool TypedNumericStringParser<T> (string text, out T value);

        // ===================================
        // Private Helper Fields
        // ===================================
        private static readonly Regex kNumericStringParser = new Regex(@"(?:-?\d*\.\d+)|(?:-?\d+)");

        private static readonly Dictionary<Type, (dynamic Min, dynamic Max)> kMinMaxes = new Dictionary<Type, (dynamic Min, dynamic Max)>
        {
            { typeof(byte), (byte.MinValue, byte.MaxValue) },
            { typeof(short), (short.MinValue, short.MaxValue) },
            { typeof(int), (int.MinValue, int.MaxValue) },
            { typeof(long), (long.MinValue, long.MaxValue) },
            { typeof(float), (float.MinValue, float.MaxValue) },
            { typeof(double), (double.MinValue, double.MaxValue) },
            { typeof(decimal), (decimal.MinValue, decimal.MaxValue) },
            { typeof(sbyte), (sbyte.MinValue, sbyte.MaxValue) },
            { typeof(uint), (uint.MinValue, uint.MaxValue) },
            { typeof(ushort), (ushort.MinValue, ushort.MaxValue) },
            { typeof(ulong), (ulong.MinValue, ulong.MaxValue) },
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
            { typeof(short), true },
            { typeof(int), false },
            { typeof(long), false },
            { typeof(float), true },
            { typeof(double), true },
            { typeof(decimal), true },
            { typeof(sbyte), true },
            { typeof(uint), true },
            { typeof(ushort), true },
            { typeof(ulong), true },
        };

        private static readonly Dictionary<Type, AdvancedStringParser> kParsers = new Dictionary<Type, AdvancedStringParser>
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

        public static T MinFor<T> () => kMinMaxes[typeof(T)].Min;
        public static dynamic MinFor (Type targetNumericType) => kMinMaxes[targetNumericType].Min;

        public static T MaxFor<T> () => kMinMaxes[typeof(T)].Max;
        public static dynamic MaxFor (Type targetNumericType) => kMinMaxes[targetNumericType].Max;

        public static dynamic PerformSafeNumericCastToTarget (dynamic value, Type targetType, out bool didCapToNumericBoundaryMin, out bool didCapToNumericBoundaryMax)
        {
            didCapToNumericBoundaryMin = false;
            didCapToNumericBoundaryMax = false;

            // If the value is already of the target type, just return it
            if (value.GetType() == targetType)
            {
                return value;
            }

            // Otherwise we're going to follow three simple steps:
            //  1. Convert the value, minimum, and maximum to their respective "largest container" (to avoid overflow)
            //  2. Compare and cap the largest container value with the largest container minimum and maximum
            //  3. Return the capped value
            //
            // In this case by "largest container", I mean numeric type that can hold the biggest and smallest numbers. This is important
            //  because otherwise you may cast an int of 5,000,000 to byte which would cause an overflow exception. So step one of byte would
            //  be to put the byte's value and byte.Minimum & byte.Maximum all expressed as ulong (the largest comperable container possible)
            //  then make the comparison & cap with the ulong values, then finally cast back the ulong to a byte.

            var minMaxOfTarget = kMinMaxes[targetType];

            // If we're a floating point based target
            if (kIsFloatingPointBasedNumericType[targetType])
            {
                // .. then double hold the largest minimum and maximum values
                double valueInLargestContainer = DoNumericCast<double>(value);
                double targetMinInLargestContainer = DoNumericCast<double>(minMaxOfTarget.Min);
                double targetMaxInLargestContainer = DoNumericCast<double>(minMaxOfTarget.Max);

                if (valueInLargestContainer < targetMinInLargestContainer)
                {
                    valueInLargestContainer = targetMinInLargestContainer;
                    didCapToNumericBoundaryMin = true;
                }
                else if (valueInLargestContainer > targetMaxInLargestContainer)
                {
                    valueInLargestContainer = targetMaxInLargestContainer;
                    didCapToNumericBoundaryMax = true;
                }

                return Convert.ChangeType(valueInLargestContainer, targetType);
            }
            // If it's an integer type, then we can only determine min/max after we decide if it's an unsigned type or not
            else if (kIsUnsignedNumericType[targetType])
            {
                // ... largest/smallest unsigned integer is ulong
                if (value < 0)
                {
                    didCapToNumericBoundaryMin = true;
                    value = 0ul;
                }

                ulong valueInLargestContainer = DoNumericCast<ulong>(value);
                ulong targetMinInLargestContainer = DoNumericCast<ulong>(minMaxOfTarget.Min);
                ulong targetMaxInLargestContainer = DoNumericCast<ulong>(minMaxOfTarget.Max);

                if (valueInLargestContainer < targetMinInLargestContainer)
                {
                    valueInLargestContainer = targetMinInLargestContainer;
                    didCapToNumericBoundaryMin = true;
                }
                else if (valueInLargestContainer > targetMaxInLargestContainer)
                {
                    valueInLargestContainer = targetMaxInLargestContainer;
                    didCapToNumericBoundaryMax = true;
                }

                return Convert.ChangeType(valueInLargestContainer, targetType);
            }
            else
            {
                // ... largest/smallest signed integer is long
                long valueInLargestContainer = DoNumericCast<long>(value);
                long targetMinInLargestContainer = DoNumericCast<long>(minMaxOfTarget.Min);
                long targetMaxInLargestContainer = DoNumericCast<long>(minMaxOfTarget.Max);

                if (valueInLargestContainer < targetMinInLargestContainer)
                {
                    valueInLargestContainer = targetMinInLargestContainer;
                    didCapToNumericBoundaryMin = true;
                }
                else if (valueInLargestContainer > targetMaxInLargestContainer)
                {
                    valueInLargestContainer = targetMaxInLargestContainer;
                    didCapToNumericBoundaryMax = true;
                }

                return Convert.ChangeType(valueInLargestContainer, targetType);
            }

            T DoNumericCast<T> (dynamic inputValue)
            {
                return (T)Convert.ChangeType(inputValue, typeof(T));
            }
        }

        public static T PerformNumericTypeSafeCapping<T> (dynamic value, dynamic min, dynamic max, out bool hadToCapAtMin, out bool hadToCapAtMax)
        {
            // First cap value to be within the numeric bounds
            Type targetType = typeof(T);

            // This will ensure value is expressed in terms of the numeric boundaries (ie an int of 5,000,00 will be cast to a byte of 255)
            dynamic castedValue = PerformSafeNumericCastToTarget(value, targetType, out hadToCapAtMin, out hadToCapAtMax);

            var targetTypedMinMax = kMinMaxes[targetType];
            dynamic castedMin = min == null ? targetTypedMinMax.Min : PerformSafeNumericCastToTarget(min, targetType, out bool _, out bool _);
            dynamic castedMax = max == null ? targetTypedMinMax.Max : PerformSafeNumericCastToTarget(max, targetType, out bool _, out bool _);

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

        public static bool TryParseString<T>(string stringValue, out T typedNumericValue)
        {
            if (kParsers[typeof(T)](stringValue, out dynamic value))
            {
                typedNumericValue = (T)value;
                return true;
            }

            typedNumericValue = default;
            return false;
        }

        public static bool TryParseString (string stringValue, Type numericType, out dynamic numericValue)
        {
            if (kParsers[numericType](stringValue, out dynamic value))
            {
                numericValue = value;
                return true;
            }

            numericValue = default;
            return false;
        }

        private static AdvancedStringParser BuildTypedNumericStringParser<T> (TypedNumericStringParser<T> typedParser)
        {
            return (string stringValue, out dynamic parsedOutputValue) =>
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

            bool _TryGetSubstringFor(string _toSearch, out string _subString)
            {
                Match match = kNumericStringParser.Match(_toSearch);
                if (match.Success)
                {
                    _subString = _toSearch.Substring(match.Index, match.Length);
                    return true;
                }

                _subString = null;
                return false;
            }
        }
    }
}
