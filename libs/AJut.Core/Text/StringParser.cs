namespace AJut.Text
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;

    /// <summary>
    /// A class that registers parsers from string to objects of a certain type.
    /// </summary>
    public class StringParser
    {
        Dictionary<Type, Func<string, object>> m_parsers = new Dictionary<Type, Func<string, object>>();

        /// <summary>
        /// Constructs a <see cref="StringParser"/> instance.
        /// </summary>
        /// <param name="registerDefaults">If <c>true</c>, all default string parsers are registered (for simple types).</param>
        public StringParser(bool registerDefaults = true)
        {
            if (registerDefaults)
            {
                this.RegisterDefaults();
            }
        }

        private void RegisterDefaults()
        {
            // Every simple type AJson writes needs a parser here. A type without one does not fail
            //  on read, it silently keeps the default its instance was constructed with.
            this.Register(Byte.Parse);
            this.Register(SByte.Parse);
            this.Register(Int16.Parse);
            this.Register(Int32.Parse);
            this.Register(Int64.Parse);
            this.Register(UInt16.Parse);
            this.Register(UInt32.Parse);
            this.Register(UInt64.Parse);

            // JSON numbers are culture-invariant text, so the floating point parsers read invariant
            //  first. Before AJson wrote numbers invariant, a comma-decimal culture wrote 0.5 as 0,5
            //  and infinity was written with the current culture's symbol, so anything invariant
            //  rejects gets a second try with the current culture. NumberStyles.Float leaves out
            //  thousands grouping on purpose: with it, invariant would take "0,5" as 5 and the
            //  fallback would never run.
            this.Register(_ParseDouble);
            this.Register(_ParseFloat);
            this.Register(_ParseDecimal);

            this.Register(bool.Parse);
            
            this.Register(char.Parse);
            this.Register(Guid.Parse);

            this.Register(IPAddress.Parse);
            this.Register(IPEndPoint.Parse);

            this.Register(s => s.Replace("\\\"", "\"")); // string

            static double _ParseDouble (string s)
            {
                return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                    ? value
                    : double.Parse(s, CultureInfo.CurrentCulture);
            }

            static float _ParseFloat (string s)
            {
                return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                    ? value
                    : float.Parse(s, CultureInfo.CurrentCulture);
            }

            static decimal _ParseDecimal (string s)
            {
                return decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal value)
                    ? value
                    : decimal.Parse(s, CultureInfo.CurrentCulture);
            }
        }

        /// <summary>
        /// Registers a function for converting from string, to a type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type to parse from</typeparam>
        /// <param name="parser">The parser function to register</param>
        public void Register<T>(Func<string, T> parser)
        {
            m_parsers[typeof(T)]= (s) => parser(s);
        }

        /// <summary>
        /// Checks if this parser can handle converting the specified type.
        /// </summary>
        /// <param name="t">The type to check</param>
        /// <returns><c>true</c> if it can convert the type from a string.</returns>
        public bool CanConvert(Type t)
        {
            if (t.IsEnum)
            {
                return true;
            }

            return m_parsers.Keys.Contains(t);
        }

        /// <summary>
        /// Converts from the given string to the specified output type
        /// </summary>
        /// <param name="source">The source string</param>
        /// <param name="outputType">The <see cref="Type"/> of instance to build</param>
        /// <returns>The instance built from the registered parsers</returns>
        public object Convert(string source, Type outputType)
        {
            if (outputType.IsEnum)
            {
                return Enum.Parse(outputType, source);
            }

            return m_parsers[outputType](source);
        }

        /// <summary>
        /// Converts from a string to the given output type
        /// </summary>
        /// <typeparam name="T">The type of instance to build</typeparam>
        /// <param name="source">The source string</param>
        /// <returns>The instance built from the registered parsers</returns>
        public T Convert<T>(string source)
        {
            return (T)Convert(source, typeof(T));
        }

        /// <summary>
        /// Tries to convert from the given string to the specified output type if it can
        /// </summary>
        /// <param name="source">The source string</param>
        /// <param name="outputType">The <see cref="Type"/> of instance to build</param>
        /// <returns>The instance built from the registered parsers if one exists, <c>null</c> otherwise.</returns>
        public object TryConvert(string source, Type outputType)
        {
            if (this.CanConvert(outputType))
            {
                return this.Convert(source, outputType);
            }

            return null;
        }

        /// <summary>
        /// Converts from a string to the given output type
        /// </summary>
        /// <typeparam name="T">The type of instance to build</typeparam>
        /// <param name="source">The source string</param>
        /// <returns>The instance built from the registered parsers if one exists, <c>null</c> otherwise</returns>
        public T TryConvert<T>(string source)
        {
            if (this.CanConvert(typeof(T)))
            {
                return this.Convert<T>(source);
            }

            return default(T);
        }
    }

}
