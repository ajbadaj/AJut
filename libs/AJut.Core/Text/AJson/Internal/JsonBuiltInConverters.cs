namespace AJut.Text.AJson
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Numerics;
    using System.Reflection;

    /// <summary>
    /// The converters AJson registers for .NET types its member walk cannot write in a shape it reads back, and the fallback
    /// for any other type that formats and parses itself as text
    /// </summary>
    internal static class JsonBuiltInConverters
    {
        /// <summary>
        /// "o" is what the DateTime and DateTimeOffset converters write. The second form also takes ISO 8601 text with fewer
        /// (or no) fractional digits, as other tools write it.
        /// </summary>
        private static readonly string[] kIso8601DateTimeFormats = { "o", "yyyy-MM-ddTHH:mm:ss.FFFFFFFK" };
        private static readonly string[] kIso8601DateTimeOffsetFormats = { "o", "yyyy-MM-ddTHH:mm:ss.FFFFFFFzzz" };

        private static readonly MethodInfo kTryParseWithMethod = typeof(JsonBuiltInConverters).GetMethod(
            nameof(TryParseWith),
            BindingFlags.NonPublic | BindingFlags.Static
        );

        // ===========================[ Public Interface Methods ]===========================
        /// <summary>
        /// A converter table holding every built-in converter, keyed by the type each converts
        /// </summary>
        public static ConcurrentDictionary<Type, JsonValueConverter> CreateTable ()
        {
            JsonValueConverter[] builtIns =
            {
                new TextConverter<bool>(_WriteBool, _TryReadBool, isUsuallyQuoted: false),
                new TextConverter<DateTime>(_WriteDateTime, _TryReadDateTime),
                new TextConverter<DateTimeOffset>(_WriteDateTimeOffset, _TryReadDateTimeOffset),
                new TextConverter<DateOnly>(_WriteDateOnly, _TryReadDateOnly),
                new TextConverter<TimeOnly>(_WriteTimeOnly, _TryReadTimeOnly),
                new TextConverter<TimeSpan>(_WriteTimeSpan, _TryReadTimeSpan),
                new TextConverter<Guid>(_WriteGuid, _TryReadGuid),
                new TextConverter<TimeZoneInfo>(_WriteTimeZone, _TryReadTimeZone, valueForUnreadableText: TimeZoneInfo.Utc),
                new TextConverter<Version>(_WriteVersion, _TryReadVersion),
                new TextConverter<Uri>(_WriteUri, _TryReadUri),
                new TextConverter<IPAddress>(_WriteIPAddress, _TryReadIPAddress),
                new TextConverter<IPEndPoint>(_WriteIPEndPoint, _TryReadIPEndPoint),
                new TextConverter<byte[]>(_WriteBytes, _TryReadBytes),
                new ComplexConverter(),
            };

            return new ConcurrentDictionary<Type, JsonValueConverter>(builtIns.ToDictionary(c => c.TargetType));

            // ------ bool: true and false, bare
            static string _WriteBool (bool value) => value ? "true" : "false";
            static bool _TryReadBool (string text, JsonInterpreterSettings settings, out bool value) => bool.TryParse(text, out value);

            // ------ DateTime: round-trip ISO 8601, culture-invariant, every tick kept, and the Kind carried in the suffix
            //  (Z for Utc, the offset for Local, nothing for Unspecified), so the reader hands back exactly the value it
            //  was given
            static string _WriteDateTime (DateTime value) => value.ToString("o", CultureInfo.InvariantCulture);
            static bool _TryReadDateTime (string text, JsonInterpreterSettings settings, out DateTime value)
            {
                // 1. Round-trip ISO 8601, which is what AJson writes. RoundtripKind hands back the Kind the text carries: Z is
                //    Utc, an offset is Local (as this machine's local time), and no suffix is Unspecified.
                bool isIso8601 = DateTime.TryParseExact(
                    text,
                    kIso8601DateTimeFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out value
                );

                if (isIso8601)
                {
                    return true;
                }

                // 2. Text written before that: the writing machine's current culture format, with no offset or Kind, which was
                //    always UTC under the old defaults. It reads the way it always did, with the invariant culture as a second
                //    try for a file written under another culture.
                DateTimeStyles oldTextStyle = settings.DefaultDateTimeParseStyle;
                return DateTime.TryParse(text, CultureInfo.CurrentCulture, oldTextStyle, out value)
                    || DateTime.TryParse(text, CultureInfo.InvariantCulture, oldTextStyle, out value);
            }

            // ------ DateTimeOffset: round-trip ISO 8601, which keeps the offset as well as every tick
            static string _WriteDateTimeOffset (DateTimeOffset value) => value.ToString("o", CultureInfo.InvariantCulture);
            static bool _TryReadDateTimeOffset (string text, JsonInterpreterSettings settings, out DateTimeOffset value)
            {
                bool isIso8601 = DateTimeOffset.TryParseExact(
                    text,
                    kIso8601DateTimeOffsetFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out value
                );

                // Text with no offset is taken as UTC, the same as DateTime text with no Kind under the default settings
                return isIso8601
                    || DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out value);
            }

            // ------ DateOnly and TimeOnly: ISO 8601, 2026-10-09 and 14:30:15.2500000. Their default formats follow the culture,
            //  and TimeOnly's drops the seconds.
            static string _WriteDateOnly (DateOnly value) => value.ToString("o", CultureInfo.InvariantCulture);
            static bool _TryReadDateOnly (string text, JsonInterpreterSettings settings, out DateOnly value)
            {
                return DateOnly.TryParseExact(text, "o", CultureInfo.InvariantCulture, DateTimeStyles.None, out value)
                    || DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
            }

            static string _WriteTimeOnly (TimeOnly value) => value.ToString("o", CultureInfo.InvariantCulture);
            static bool _TryReadTimeOnly (string text, JsonInterpreterSettings settings, out TimeOnly value)
            {
                return TimeOnly.TryParseExact(text, "o", CultureInfo.InvariantCulture, DateTimeStyles.None, out value)
                    || TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
            }

            // ------ TimeSpan: the constant format, [-][d.]hh:mm:ss[.fffffff], which is culture-invariant
            static string _WriteTimeSpan (TimeSpan value) => value.ToString("c", CultureInfo.InvariantCulture);
            static bool _TryReadTimeSpan (string text, JsonInterpreterSettings settings, out TimeSpan value)
            {
                return TimeSpan.TryParseExact(text, "c", CultureInfo.InvariantCulture, out value)
                    || TimeSpan.TryParse(text, CultureInfo.CurrentCulture, out value);
            }

            // ------ The rest are each type's own text
            static string _WriteGuid (Guid value) => value.ToString();
            static bool _TryReadGuid (string text, JsonInterpreterSettings settings, out Guid value) => Guid.TryParse(text, out value);

            static string _WriteTimeZone (TimeZoneInfo value) => value.Id;
            static bool _TryReadTimeZone (string text, JsonInterpreterSettings settings, out TimeZoneInfo value)
            {
                return TimeZoneInfo.TryFindSystemTimeZoneById(text, out value);
            }

            static string _WriteVersion (Version value) => value.ToString();
            static bool _TryReadVersion (string text, JsonInterpreterSettings settings, out Version value) => Version.TryParse(text, out value);

            // A relative Uri has no AbsoluteUri, so a Uri is written as the text it was made from
            static string _WriteUri (Uri value) => value.OriginalString;
            static bool _TryReadUri (string text, JsonInterpreterSettings settings, out Uri value)
            {
                return Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out value);
            }

            static string _WriteIPAddress (IPAddress value) => value.ToString();
            static bool _TryReadIPAddress (string text, JsonInterpreterSettings settings, out IPAddress value)
            {
                return IPAddress.TryParse(text, out value);
            }

            static string _WriteIPEndPoint (IPEndPoint value) => value.ToString();
            static bool _TryReadIPEndPoint (string text, JsonInterpreterSettings settings, out IPEndPoint value)
            {
                return IPEndPoint.TryParse(text, out value);
            }

            // ------ byte[]: base64, one value instead of an array of numbers. An array written before that still reads, since
            //  only a single value goes to this converter.
            static string _WriteBytes (byte[] value) => Convert.ToBase64String(value);
            static bool _TryReadBytes (string text, JsonInterpreterSettings settings, out byte[] value)
            {
                // Base64 writes every 3 bytes as 4 characters, which is how big a buffer its text needs
                const int kBytesPerBase64Block = 3;
                const int kCharactersPerBase64Block = 4;

                value = new byte[(text.Length * kBytesPerBase64Block) / kCharactersPerBase64Block];
                if (Convert.TryFromBase64String(text, value, out int bytesWritten))
                {
                    Array.Resize(ref value, bytesWritten);
                    return true;
                }

                value = null;
                return false;
            }
        }

        /// <summary>
        /// The converter for a type with no converter of its own that writes and reads itself as text: one implementing both
        /// IFormattable and IParsable of itself, the way .NET's own scalars do (Int128, BigInteger, Half). A type that also
        /// implements INumberBase is written bare, as a number. A simple type (a primitive, a string, an enum) is left to the
        /// simple value path.
        /// </summary>
        /// <returns>The converter, or null when <paramref name="type"/> does not qualify</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2070",
            Justification = "Reflection-path fallback; generated serializers find IParsable at compile time instead.")]
        public static JsonValueConverter TryMakeParsableConverter (Type type)
        {
            if (type.IsSimpleType()
                || type.IsEnum
                || type.IsGenericTypeDefinition
                || !typeof(IFormattable).IsAssignableFrom(type)
                || !_ImplementsGenericOfSelf(type, typeof(IParsable<>)))
            {
                return null;
            }

            return new ParsableConverter(type, isUsuallyQuoted: !_ImplementsGenericOfSelf(type, typeof(INumberBase<>)));

            static bool _ImplementsGenericOfSelf (Type _type, Type _genericInterface)
            {
                foreach (Type implemented in _type.GetInterfaces())
                {
                    if (implemented.IsGenericType
                        && implemented.GetGenericTypeDefinition() == _genericInterface
                        && implemented.GenericTypeArguments[0] == _type)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        // ===========================[ Helper Methods ]===========================
        // Called through MakeGenericMethod, since a static abstract interface member cannot be invoked through reflection
        //  directly. The constraint is what lets T.TryParse call the type's own implementation, explicit or not.
        private static bool TryParseWith<T> (string text, out object value) where T : IParsable<T>
        {
            bool parsed = T.TryParse(text, CultureInfo.InvariantCulture, out T result);
            value = result;
            return parsed;
        }

        // ===========================[ Subclasses/structs ]===========================
        /// <summary>
        /// Reads text for one type
        /// </summary>
        private delegate bool TextParser<T> (string text, JsonInterpreterSettings settings, out T value);

        /// <summary>
        /// A scalar converter made from a pair of functions
        /// </summary>
        private sealed class TextConverter<T> : JsonScalarConverter
        {
            private readonly Func<T, string> m_write;
            private readonly TextParser<T> m_read;
            private readonly T m_valueForUnreadableText;

            /// <param name="write">Makes the text</param>
            /// <param name="read">Reads the text back</param>
            /// <param name="isUsuallyQuoted">False to write the text bare, as a number is</param>
            /// <param name="valueForUnreadableText">What text that cannot be read reads as, default(T) unless given</param>
            public TextConverter (Func<T, string> write, TextParser<T> read, bool isUsuallyQuoted = true, T valueForUnreadableText = default)
                : base(typeof(T), isUsuallyQuoted)
            {
                m_write = write;
                m_read = read;
                m_valueForUnreadableText = valueForUnreadableText;
            }

            public override string ToJsonText (object instance) => m_write((T)instance);

            protected override object GetValueForUnreadableText (Type fullTarget) => m_valueForUnreadableText;

            public override bool TryReadJsonText (string text, Type fullTarget, JsonInterpreterSettings settings, out object value)
            {
                if (text != null && m_read(text, settings, out T read))
                {
                    value = read;
                    return true;
                }

                value = null;
                return false;
            }
        }

        /// <summary>
        /// Complex as a document of its two parts, { "Real": 1.5, "Imaginary": -2 }. The member walk would also write its
        /// Magnitude and Phase, which only restate the two, and could not read it back at all.
        /// </summary>
        private sealed class ComplexConverter : JsonValueConverter
        {
            private const string kRealKey = nameof(Complex.Real);
            private const string kImaginaryKey = nameof(Complex.Imaginary);

            public ComplexConverter () : base(typeof(Complex), eJsonValueShape.Document)
            {
            }

            /// <summary>
            /// Writes { "Real": ..., "Imaginary": ... }, both as bare numbers
            /// </summary>
            public override void Write (object instance, JsonBuilder target)
            {
                Complex value = (Complex)instance;
                JsonBuilder document = StartDocument(target);
                document.AddProperty(kRealKey, value.Real, isUsuallyQuoted: false);
                document.AddProperty(kImaginaryKey, value.Imaginary, isUsuallyQuoted: false);
            }

            public override bool CanRead (JsonValue json) => json.IsDocument;

            public override object Read (Type fullTarget, JsonValue json, JsonInterpreterSettings settings, Json owner)
            {
                JsonDocument document = (JsonDocument)json;
                return new Complex(_ReadPart(kRealKey), _ReadPart(kImaginaryKey));

                // A missing part is 0, and an unreadable one is reported
                double _ReadPart (string _key)
                {
                    JsonValue part = document.ValueFor(_key);
                    if (part == null)
                    {
                        return 0.0;
                    }

                    if (double.TryParse(part.StringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double read))
                    {
                        return read;
                    }

                    owner?.AddError($"Could not read '{part.StringValue}' as the {_key} part of a Complex, it is left at 0");
                    return 0.0;
                }
            }
        }

        /// <summary>
        /// The fallback converter for a type that formats and parses itself as text, made by <see cref="TryMakeParsableConverter"/>
        /// </summary>
        private sealed class ParsableConverter : JsonScalarConverter
        {
            private readonly TryParseText m_tryParse;

            [UnconditionalSuppressMessage("AOT", "IL3050",
                Justification = "Reflection-path fallback; generated serializers call the type's TryParse directly instead.")]
            public ParsableConverter (Type type, bool isUsuallyQuoted) : base(type, isUsuallyQuoted)
            {
                m_tryParse = kTryParseWithMethod.MakeGenericMethod(type).CreateDelegate<TryParseText>();
            }

            private delegate bool TryParseText (string text, out object value);

            public override string ToJsonText (object instance)
            {
                return ((IFormattable)instance).ToString(null, CultureInfo.InvariantCulture);
            }

            public override bool TryReadJsonText (string text, Type fullTarget, JsonInterpreterSettings settings, out object value)
            {
                return m_tryParse(text, out value);
            }
        }
    }
}
