namespace AJut.UX
{
    using System;
    using System.Globalization;

    // ===========[ CoerceParsing ]==============================================
    // The toolkit-free half of each UX package's CoerceUtils: reading corner and side values
    // from 1, 2 or 4 numbers, enums from their names, and absolute uris from text. Each package
    // builds its own types (CornerRadius, Thickness, a native inset) from what this reads, and
    // resolves a uri that is not absolute against its own app scheme.
    public static class CoerceParsing
    {
        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Reads four corner values from 1, 2 or 4 numbers separated by commas or spaces: one is uniform, two are top then
        /// bottom, four are top left, top right, bottom right, bottom left
        /// </summary>
        /// <exception cref="FormatException">The text is not 1, 2 or 4 numbers</exception>
        public static Corners ParseCorners (object originalValue)
        {
            try
            {
                double[] values = ParseNumbers(originalValue.ToString()!);
                if (values.Length == 1)
                {
                    return new Corners(values[0], values[0], values[0], values[0]);
                }

                if (values.Length == 2)
                {
                    return new Corners(values[0], values[0], values[1], values[1]);
                }

                if (values.Length == 4)
                {
                    return new Corners(values[0], values[1], values[2], values[3]);
                }

                throw new InvalidOperationException("Invalid CornerRadius specification, must be 1=uniform || 2=top,bottom || 4=topLeft,topRight,bottomRight,bottomLeft (you may also use spaces instead of commas like Ian)");
            }
            catch (Exception exc)
            {
                throw new FormatException("Error in CornerRadius coersion. Input value was \"" + originalValue.ToString() + "\", which produced an exception.", exc);
            }
        }

        /// <summary>
        /// Reads four side values from 1, 2 or 4 numbers separated by commas or spaces: one is uniform, two are left and
        /// right then top and bottom, four are left, top, right, bottom
        /// </summary>
        /// <exception cref="FormatException">The text is not 1, 2 or 4 numbers</exception>
        public static Sides ParseSides (object originalValue)
        {
            try
            {
                double[] values = ParseNumbers(originalValue.ToString()!);
                if (values.Length == 1)
                {
                    return new Sides(values[0], values[0], values[0], values[0]);
                }

                if (values.Length == 2)
                {
                    return new Sides(values[0], values[1], values[0], values[1]);
                }

                if (values.Length == 4)
                {
                    return new Sides(values[0], values[1], values[2], values[3]);
                }

                throw new InvalidOperationException("Invalid Thickness specification, must be 1=uniform || 2=left/right,top/bottom || 4=left,top,right,bottom (you may also use spaces instead of commas like Ian)");
            }
            catch (Exception exc)
            {
                throw new FormatException("Error in Thickness coersion. Input value was \"" + originalValue.ToString() + "\", which produced an exception.", exc);
            }
        }

        /// <summary>The value when it already is a <typeparamref name="TEnum"/>, parsed from its name when it is a string, otherwise the default</summary>
        public static TEnum CoerceEnumFrom<TEnum> (object? originalValue) where TEnum : Enum
        {
            if (originalValue is TEnum)
            {
                return (TEnum)originalValue;
            }

            if (originalValue is string strValue)
            {
                return (TEnum)Enum.Parse(typeof(TEnum), strValue);
            }

            return default!;
        }

        /// <summary>
        /// The value as an absolute uri when it already is a <see cref="Uri"/> or its text reads as an absolute one. Otherwise
        /// null, with <paramref name="text"/> holding the value's text for the toolkit to resolve against its own app scheme
        /// (null when there is no value at all).
        /// </summary>
        public static Uri? CoerceAbsoluteUri (object? originalValue, out string? text)
        {
            text = null;
            if (null == originalValue)
            {
                return null;
            }

            // Is it already a URI?
            if (originalValue is Uri originalAsUri)
            {
                return originalAsUri;
            }

            // Can we make a URI out of it as a string?
            text = originalValue.ToString();
            if (text != null && Uri.TryCreate(text, UriKind.Absolute, out Uri? newUri))
            {
                return newUri;
            }

            return null;
        }

        // ===========[ Helper Methods ]========================================
        // Invariant, since XAML and code write these numbers with a period whatever culture the app runs in
        private static double[] ParseNumbers (string text)
        {
            string[] csv = text.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var values = new double[csv.Length];
            for (int index = 0; index < csv.Length; ++index)
            {
                values[index] = double.Parse(csv[index], CultureInfo.InvariantCulture);
            }

            return values;
        }

        // ===========[ Subclasses/structs ]====================================
        /// <summary>Four corner values, as a corner radius takes them</summary>
        public readonly record struct Corners (double TopLeft, double TopRight, double BottomRight, double BottomLeft);

        /// <summary>Four side values, as a thickness takes them</summary>
        public readonly record struct Sides (double Left, double Top, double Right, double Bottom);
    }
}
