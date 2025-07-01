namespace AJut.UX
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Microsoft.UI;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Media;
    using Microsoft.UI.Xaml.Media.Imaging;
    using Windows.UI;

    /// <summary>
    /// A set of utilities for ease of coercion
    /// </summary>
    public static class CoerceUtils
    {
        /// <summary>
        /// Callback for color coercion.
        /// </summary>
        /// <param name="obj">The dependency object.</param>
        /// <param name="originalValue">The original value.</param>
        /// <returns>A color coerced object.</returns>
        public static object CallbackForColor (DependencyObject obj, object originalValue)
        {
            return CoerceColorFrom(originalValue);
        }

        /// <summary>
        /// Callback for brush coercion.
        /// </summary>
        /// <param name="obj">The dependency object.</param>
        /// <param name="originalValue">The original value.</param>
        /// <returns>A brush coerced object.</returns>
        public static object CallbackForBrush (DependencyObject obj, object originalValue)
        {
            return CoerceBrushFrom(originalValue);
        }

        /// <summary>
        /// Callback for Uri coercion (used with dependency properties)
        /// </summary>
        /// <param name="obj">The dependency object target.</param>
        /// <param name="originalValue">The original value.</param>
        /// <returns>An object of what was coerced.</returns>
        /// <remarks>If you want to reference a uri from a different assembly, make sure to use the full uri scheme in your xaml</remarks>
        public static object CallbackForUri (DependencyObject obj, object originalValue)
        {
            return CoerceUriSourceFrom(originalValue);
        }

        /// <summary>
        /// Callback for <see cref="ImageSource"/> coercion.
        /// </summary>
        /// <param name="obj">The dependency object.</param>
        /// <param name="originalValue">The original value.</param>
        /// <returns>An image source coerced object.</returns>
        public static object CallbackForImageSource (DependencyObject obj, object originalValue)
        {
            return CoerceImageSourceFrom(originalValue);
        }

        /// <summary>
        /// Callback for <see cref="CornerRadius"/> coercion.
        /// </summary>
        /// <param name="obj">The dependency object.</param>
        /// <param name="originalValue">The original value.</param>
        /// <returns>A corner radius coerced object.</returns>
        public static object CallbackForCornerRadius (DependencyObject obj, object originalValue)
        {
            CornerRadius? cr = CoerceCornerRadiusFrom(originalValue);
            return cr ?? new CornerRadius(0);
        }

        /// <summary>
        /// Callback for <see cref="Thickness"/> coercion.
        /// </summary>
        /// <param name="obj">The dependency object.</param>
        /// <param name="originalValue">The original value.</param>
        /// <returns>A thickness coerced object.</returns>
        public static object CallbackForThickness (DependencyObject obj, object originalValue)
        {
            Thickness? thickness = CoerceThicknessFrom(originalValue);
            return thickness ?? new Thickness(0);
        }

        /// <summary>
        /// Callback for <see cref="FontFamily"/> coercion.
        /// </summary>
        /// <param name="obj">The dependency object.</param>
        /// <param name="originalValue">The original value.</param>
        /// <returns>A font family coerced object.</returns>
        public static object CallbackForFontFamily (DependencyObject obj, object originalValue)
        {
            return CoerceFontFamilyFrom(originalValue);
        }

        /// <summary>
        /// Coerces the original value to a Uri if possible.
        /// </summary>
        /// <param name="originalValue">The original value.</param>
        /// <param name="assemblyShortName">Short name of the assembly (optional).</param>
        /// <returns>The coereced <see cref="Uri"/>.</returns>
        public static Uri CoerceUriSourceFrom (object originalValue, string assemblyShortName = null)
        {
            if (null == originalValue)
            {
                return null;
            }

            // Is it already a URI?
            var originalAsUri = originalValue as Uri;
            if (originalAsUri != null)
            {
                return originalAsUri;
            }

            // Can we make a URI out of it as a string?
            string strValue = originalValue.ToString();
            if (null == strValue)
            {
                return null;
            }

            if (Uri.TryCreate(strValue, UriKind.Absolute, out Uri newUri))
            {
                return newUri;
            }

            // Can we generate a fully resolved pack URI given the path?
            if (assemblyShortName == null)
            {
                // Some partial URIs are specified as /AssemblyShortName;component/path.png
                //  This does not count as a fully qualified packurl so the TryCreate above will fail
                //  but it does give us an assembly short name.
                const string partiallyQualifiedPathSegment = ";component/";

                int ind = strValue.IndexOf(partiallyQualifiedPathSegment);
                if (ind != -1)
                {
                    assemblyShortName = strValue.Remove(ind).TrimStart('/');
                    strValue = strValue.Substring(ind + partiallyQualifiedPathSegment.Length);
                }
                // Otherwise just guess
                else
                {
                    assemblyShortName = Assembly.GetEntryAssembly().GetName().Name;
                }
            }

            string newAbsoluteUriString = string.Format("pack://application:,,,/{0};component/{1}", assemblyShortName, strValue.TrimStart('/'));
            if (Uri.TryCreate(newAbsoluteUriString, UriKind.Absolute, out newUri))
            {
                return newUri;
            }

            return null;
        }

        public static TEnum CoerceEnumFrom<TEnum> (object originalValue) where TEnum : Enum
        {
            if (originalValue is TEnum)
            {
                return (TEnum)originalValue;
            }

            if (originalValue is string strValue)
            {
                return (TEnum)Enum.Parse(typeof(TEnum), strValue);
            }

            return default;
        }

        private static Lazy<Dictionary<string, Color>> g_allNamedColors = new Lazy<Dictionary<string, Color>>(() =>
        {
            Dictionary<string, Color> colorMap = new Dictionary<string, Color>();
            var allColorProps = typeof(Colors).GetProperties(BindingFlags.Public | BindingFlags.Static);
            foreach (PropertyInfo colorProp in allColorProps)
            {
                colorMap.Add(colorProp.Name, (Color)colorProp.GetValue(null));
            }

            return colorMap;
        });

        /// <summary>
        /// Coercion utility for <see cref="Color"/> generation.
        /// </summary>
        /// <param name="originalValue">The original value.</param>
        /// <returns>A color coerced out of the original value.</returns>
        public static Color CoerceColorFrom (object originalValue)
        {
            if (originalValue == null)
            {
                return Colors.Black;
            }

            if (originalValue is Color)
            {
                return (Color)originalValue;
            }
            else if (originalValue is string strValue && TryGetColorFromString(strValue, out Color color))
            {
                return color;
            }

            return Colors.Black;
        }

        /// <summary>
        /// Try and rationalize a string as a color
        /// </summary>
        /// <param name="value">The string to interpret</param>
        /// <param name="color">The found color</param>
        /// <returns>a bool indicating if a color was found</returns>
        public static bool TryGetColorFromString (string value, out Color color)
        {
            value = value.Trim();
            if (ColorHelper.TryGetColorFromHex(!value.StartsWith("#") ? "#" + value : value, out color))
            {
                return true;
            }

            if (g_allNamedColors.Value.TryGetValue(value, out color))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Coercion utility for <see cref="Brush"/> generation.
        /// </summary>
        /// <param name="originalValue">The original value.</param>
        /// <returns>A brush coerced out of the original value.</returns>
        public static Brush CoerceBrushFrom (object originalValue)
        {
            if (originalValue == null)
            {
                return null;
            }

            if (originalValue is Brush)
            {
                return originalValue as Brush;
            }
            else if (originalValue is Color)
            {
                return new SolidColorBrush((Color)originalValue);
            }
            else if (originalValue is string strValue)
            {
                return new SolidColorBrush(CoerceColorFrom(strValue));
            }

            return new SolidColorBrush(Colors.Black);
        }

        /// <summary>
        /// Coercion utility for <see cref="ImageSource"/> generation for images that are part of the indicated assembly.
        /// </summary>
        /// <param name="originalValue">The original value.</param>
        /// <param name="assemblyShortName">The short name of the assembly to look up (default is null which will cause the entry assembly to be used)</param>
        /// <returns>An ImageSource coerced out of the original value.</returns>
        public static ImageSource CoerceImageSourceFrom (object originalValue, string assemblyShortName = null)
        {
            if (null == originalValue)
            {
                return null;
            }

            if (originalValue is ImageSource originalAsImageSource)
            {
                return originalAsImageSource;
            }

            Uri uri = CoerceUriSourceFrom(originalValue, assemblyShortName);
            if (null == uri)
            {
                return null;
            }

            return new BitmapImage(uri);
        }

        /// <summary>
        /// Coercion utility for <see cref="CornerRadius"/> generation.
        /// </summary>
        /// <param name="originalValue">The original value.</param>
        /// <returns>A corner radius coerced out of the original value.</returns>
        /// <exception cref="System.InvalidOperationException">Invalid comma separated values split, must be 1=uniform || 2=top,bottom || 4=topLeft, topRight, bottomRight, bottomLeft</exception>
        /// <exception cref="System.FormatException">Exception from parsing comma separated values.</exception>
        public static CornerRadius? CoerceCornerRadiusFrom (object originalValue)
        {
            if (originalValue is CornerRadius)
            {
                return (CornerRadius?)originalValue;
            }

            try
            {
                string[] csv = originalValue.ToString().Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (csv.Length == 1)
                {
                    return new CornerRadius(double.Parse(csv[0]));
                }

                if (csv.Length == 2)
                {
                    double top = double.Parse(csv[0]);
                    double bottom = double.Parse(csv[1]);
                    return new CornerRadius(top, top, bottom, bottom);
                }

                if (csv.Length == 4)
                {
                    return new CornerRadius(double.Parse(csv[0]), double.Parse(csv[1]), double.Parse(csv[2]), double.Parse(csv[3]));
                }

                throw new InvalidOperationException("Invalid CornerRadius specification, must be 1=uniform || 2=top,bottom || 4=topLeft,topRight,bottomRight,bottomLeft (you may also use spaces instead of commas like Ian)");
            }
            catch (Exception exc)
            {
                throw new FormatException("Error in CornerRadius coersion. Input value was \"" + originalValue.ToString() + "\", which produced an exception.", exc);
            }
        }

        /// <summary>
        /// Coercion utility for <see cref="Thickness"/> generation.
        /// </summary>
        /// <param name="originalValue">The original value.</param>
        /// <returns>A thickness coerced out of the original value.</returns>
        /// <exception cref="System.InvalidOperationException">Invalid CSV specification, must be 1=uniform || 2=left/right,top/bottom || 4=left,top,right,bottom</exception>
        /// <exception cref="System.FormatException">Invalid CSV specification.</exception>
        public static Thickness? CoerceThicknessFrom (object originalValue)
        {
            if (originalValue == null)
            {
                return null;
            }

            if (originalValue is Thickness)
            {
                return (Thickness?)originalValue;
            }

            try
            {
                string[] csv = originalValue.ToString().Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (csv.Length == 1)
                {
                    return new Thickness(double.Parse(csv[0]));
                }

                if (csv.Length == 2)
                {
                    double hor = double.Parse(csv[0]);
                    double ver = double.Parse(csv[1]);
                    return new Thickness(hor, ver, hor, ver);
                }

                if (csv.Length == 4)
                {
                    return new Thickness(double.Parse(csv[0]), double.Parse(csv[1]), double.Parse(csv[2]), double.Parse(csv[3]));
                }

                throw new InvalidOperationException("Invalid Thickness specification, must be 1=uniform || 2=left/right,top/bottom || 4=left,top,right,bottom (you may also use spaces instead of commas like Ian)");
            }
            catch (Exception exc)
            {
                throw new FormatException("Error in Thickness coersion. Input value was \"" + originalValue.ToString() + "\", which produced an exception.", exc);
            }
        }

        /// <summary>
        /// Coercion utility for <see cref="FontFamily"/> generation.
        /// </summary>
        /// <param name="originalValue">The original value.</param>
        /// <returns>A font family coerced out of the original value.</returns>
        public static FontFamily CoerceFontFamilyFrom (object originalValue)
        {
            if (originalValue == null)
            {
                return null;
            }

            if (originalValue is FontFamily fontFamilyValue)
            {
                return fontFamilyValue;
            }

            string fontFamilyNameStr = originalValue.ToString();
            if (null == fontFamilyNameStr)
            {
                return null;
            }

            return new FontFamily(fontFamilyNameStr);
        }
    }
}