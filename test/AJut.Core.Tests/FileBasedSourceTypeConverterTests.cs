namespace AJut.Core.UnitTests
{
    using System;
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;
    using AJut.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class FileBasedSourceTypeConverterTests
    {
        [TestMethod]
        public void FileBasedSourceTypeConverter_FileUri_OpensThePathItWasGiven ()
        {
            // A name with capitals: off Windows a lowercased or backslashed copy of the path names no file
            string folder = Path.Combine(Path.GetTempPath(), $"AJutConverterTest-{Guid.NewGuid():N}");
            string filePath = Path.Combine(folder, "Source.TXT");
            Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllText(filePath, "the contents");

                object result = new TextFileConverter().ConvertFrom(null, CultureInfo.InvariantCulture, new Uri(filePath));

                Assert.AreEqual("the contents", result, "A file uri converts from the file it names");
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        private class TextFileConverter : FileBasedSourceTypeConverter
        {
            protected override object ConvertFrom (ITypeDescriptorContext context, CultureInfo culture, Stream value)
            {
                using var reader = new StreamReader(value);
                return reader.ReadToEnd();
            }
        }
    }
}
