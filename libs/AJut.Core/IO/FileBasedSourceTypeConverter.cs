namespace AJut.IO
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;

    public abstract class FileBasedSourceTypeConverter : TypeConverter
    {
        // ==========================[ Basic Implementation ] ===========================

        public override bool CanConvertFrom (ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string) || sourceType == typeof(Stream) || sourceType == typeof(Uri) || sourceType == typeof(byte[]))
            {
                return true;
            }

            return base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom (ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            try
            {
                if (value == null)
                {
                    throw GetConvertFromException(value);
                }

                if (value is Uri uriValue)
                {
                    return this.ConvertFrom(context, culture, uriValue);
                }
                else if (value is string strValue && strValue.IsNotNullOrEmpty())
                {
                    return this.ConvertFrom(context, culture, strValue);
                }

                else if (value is byte[] bytesValue)
                {
                    return this.ConvertFrom(context, culture, bytesValue);
                }
                else if (value is Stream streamValue)
                {
                    return this.ConvertFrom(context, culture, streamValue);
                }

                return base.ConvertFrom(context, culture, value);
            }
            catch (Exception e)
            {
                Debug.Assert(true, e.ToString());
                Logger.LogError("Failed to create file-based source type converter");

                // We want to rethrow the exception in the case we can't handle it.
                throw;
            }
        }

        // ==========================[ Overridable interface ] ===========================

        // - stream
        protected abstract object ConvertFrom (ITypeDescriptorContext context, CultureInfo culture, Stream value);

        // - uri

        protected virtual object ConvertFrom (ITypeDescriptorContext context, CultureInfo culture, Uri uri)
        {
            if (uri.Scheme == Uri.UriSchemeFile && uri.LocalPath is string path)
            {
                path = PathHelpers.NormalizePath(path);
                if (!File.Exists(path))
                {
                    _RuhRoh();
                    return null;
                }

                using (var fileStream = File.OpenRead(path))
                {
                    MemoryStream memStream = new MemoryStream();
                    fileStream.CopyTo(memStream);
                    memStream.Position = 0;
                    return this.ConvertFrom(context, culture, memStream);
                }
            }

            _RuhRoh();
            return null;

            void _RuhRoh ()
            {
                Debug.Assert(true, $"Path {uri} not understood for file based source converter");
                Logger.LogError($"Path {uri} not understood for file based source converter");
            }
        }

        // - string
        protected virtual object ConvertFrom (ITypeDescriptorContext context, CultureInfo culture, string strValue)
        {
            return this.ConvertFrom(context, culture, new Uri(Path.GetFullPath(strValue)));
        }

        // - bytes
        protected virtual object ConvertFrom (ITypeDescriptorContext context, CultureInfo culture, byte[] bytesValue)
        {
            MemoryStream memStream = new MemoryStream(bytesValue);
            return ConvertFrom(memStream);
        }

    }
}
