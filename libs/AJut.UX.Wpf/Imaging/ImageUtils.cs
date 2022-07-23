namespace AJut.UX
{
    using System.Drawing.Imaging;
    using System.IO;
    using System.Windows.Media.Imaging;

    public static class ImageUtils
    {
        public static bool AreBytesPngHeader (byte[] imgdata)
        {
            if (imgdata.Length < 8)
            {
                return false;
            }

            return 0x89 == imgdata[0]
                && 0x50 == imgdata[1]
                && 0x4E == imgdata[2]
                && 0x47 == imgdata[3]
                && 0x0D == imgdata[4]
                && 0x0A == imgdata[5]
                && 0x1A == imgdata[6]
                && 0x0A == imgdata[7];
        }

        public static bool AreBytesPngHeader (ulong bytesAsLong)
        {
            return bytesAsLong == 727905341920923785;
        }

        public static BitmapImage GetImageSourceFrom (System.Drawing.Image image)
        {
            BitmapImage imageSource = new BitmapImage();
            Stream bitmapStream = new MemoryStream();
            image.Save(bitmapStream, ImageFormat.Png);
            bitmapStream.Seek(0, SeekOrigin.Begin);

            imageSource.BeginInit();
            imageSource.StreamSource = bitmapStream;
            imageSource.EndInit();
            if (imageSource.CanFreeze)
            {
                imageSource.Freeze();
            }

            return imageSource;
        }

        public static BitmapImage GetImageSourceFrom (string filePath)
        {
            return (BitmapImage)CoerceUtils.CoerceImageSourceFrom(filePath);
        }

        public static void WriteTo (this BitmapSource image, Stream stream)
        {
            var encoder = new PngBitmapEncoder();
            var frame = BitmapFrame.Create(image);
            encoder.Frames.Add(frame);
            encoder.Save(stream);
        }
    }
}
