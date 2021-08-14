namespace AJut.Application.ImageUtils
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    public static class IconHelper
    {

        public static IcoHeader ReadIcoHeaderFrom (string iconPath)
        {
            using (FileStream stream = File.OpenRead(iconPath))
            {
                return ReadIcoHeaderFrom(stream);
            }
        }

        public static IcoHeader ReadIcoHeaderFrom (Stream iconStream)
        {
            if (iconStream.Position != 0)
            {
                iconStream.Seek(0, SeekOrigin.Begin);
            }

            BinaryReader reader = new BinaryReader(iconStream);
            iconStream.Seek(2, SeekOrigin.Current); // reserved, zero

            var header = new IcoHeader
            {
                Type = reader.ReadUInt16() == 1 ? eIcoType.IcoImages : eIcoType.Cursor,
                ImageCount = reader.ReadUInt16()
            };

            for (int iconCount = 0; iconCount < header.ImageCount; ++iconCount)
            {
                header.Entries.Add(
                    header.Type == eIcoType.IcoImages
                        ? new IconEntryInfo(reader)
                        : new CursorEntryInfo(reader)
                );
            }

            return header;
        }

        public static IcoImagePart[] ReadImagePieces (IcoHeader header, Stream iconStream)
        {
            iconStream.Seek(0, SeekOrigin.Begin);
            BinaryReader reader = new BinaryReader(iconStream);
            byte[] pngHeaderBuffer = new byte[8];
            byte[] imgDataTransferBuffer = new byte[header.Entries.Select(e => e.SizeInBytes).Max()];

            List<IcoImagePart> found = new List<IcoImagePart>();
            foreach (var entry in header.Entries)
            {
                // Seek to the start, & read the first 8 bytes so we can check if we're reading a png or bmp
                iconStream.Seek(entry.DataOffsetFromFileStart, SeekOrigin.Begin);
                bool isPng = ImageUtils.AreBytesPngHeader(reader.ReadUInt64());

                // Seek to the start again so we can actually read and parse the data
                iconStream.Seek(entry.DataOffsetFromFileStart, SeekOrigin.Begin);

                // Evaluate a PNG
                if (isPng)
                {
                    int bytesRead = reader.Read(imgDataTransferBuffer, 0, (int)entry.SizeInBytes);
                    Debug.Assert(entry.SizeInBytes == bytesRead);
                    using (MemoryStream transfer = new MemoryStream(imgDataTransferBuffer, 0, (int)entry.SizeInBytes))
                    {
                        Image png = Image.FromStream(transfer);
                        _AddToFound(entry, png, eIcoImageType.Png);
                    }
                }

                // Evaluate a BMP
                else
                {
#if false

                    const int kBitmapFileHeaderSize = 14;
                    const int kBitmapInfoHeaderSize = 40;
                    int totalFileSize = kBitmapFileHeaderSize + (int)entry.SizeInBytes;
                    using MemoryStream bmpMemoryStream = new MemoryStream(totalFileSize);

                    // ========[ Write the BITMAPFILEHEADER ]===========
                    BinaryWriter bmpMemoryWriter = new BinaryWriter(bmpMemoryStream);
                    byte[] bytes123 = "BM".Select(c => (byte)c).ToArray();
                    bmpMemoryWriter.Write("BM".Select(c => (byte)c).ToArray());
                    bmpMemoryWriter.Write(totalFileSize);
                    bmpMemoryWriter.Write(0);
                    bmpMemoryWriter.Write(kBitmapFileHeaderSize + kBitmapInfoHeaderSize);

                    bmpMemoryWriter.Write(40);
                    bmpMemoryWriter.Write(entry.ImageWidth);
                    bmpMemoryWriter.Write(entry.ImageHeight);
                    if (entry is IconEntryInfo iconEntry)
                    {
                        bmpMemoryWriter.Write(iconEntry.ColorPlanes);
                        bmpMemoryWriter.Write(iconEntry.BitsPerPixel);
                        bmpMemoryWriter.Write(0 /* Compression */);
                        bmpMemoryWriter.Write(0 /* Image Size After Decompression - 0 for uncompressed */);
                        bmpMemoryWriter.Write(iconEntry.ImageWidth /* Pixels Per Meter: Horizontal */);
                        bmpMemoryWriter.Write(iconEntry.ImageHeight /* Pixels Per Meter: Horizontal */);
                        bmpMemoryWriter.Write(iconEntry.ColorPaletteColorCount);
                    }
                    else
                    {
                        bmpMemoryWriter.Write(0 /* Color Planes */);
                        bmpMemoryWriter.Write(32 /* Bits Per Pixel */);
                        bmpMemoryWriter.Write(0 /* Compression */);
                        bmpMemoryWriter.Write(0 /* Image Size After Decompression - 0 for uncompressed */);
                        bmpMemoryWriter.Write(entry.ImageWidth /* Pixels Per Meter: Horizontal */);
                        bmpMemoryWriter.Write(entry.ImageHeight /* Pixels Per Meter: Horizontal */);
                        bmpMemoryWriter.Write(entry.ImageWidth * entry.ImageHeight /*colors used*/);
                    }
                    bmpMemoryWriter.Write(0 /*important colors: 0 == all*/);
endif
                    int bytesRead = reader.Read(imgDataTransferBuffer, 0, (int)entry.SizeInBytes);
                    Debug.Assert(entry.SizeInBytes == bytesRead);

                    // ========[ Write the body ]===========
                    bmpMemoryWriter.Write(imgDataTransferBuffer, 0, (int)entry.SizeInBytes);

                    // Create something we can work with, and add it!
                    bmpMemoryStream.Seek(0, SeekOrigin.Begin);
                    using (var DEBUG_TEMP_WRITE_STREAM = File.OpenWrite(@"C:\_dev\AJut\_ignore\test123.bmp"))
                    {
                        bmpMemoryStream.WriteTo(DEBUG_TEMP_WRITE_STREAM);
                        bmpMemoryStream.Seek(0, SeekOrigin.Begin);
                    }
                    Image bmp = Image.FromStream(bmpMemoryStream);
                    _AddToFound(entry, bmp, eIcoImageType.Bmp);
#endif

                    int bitsPerPixel = entry is IconEntryInfo casted ? casted.BitsPerPixel : 32;
                    int pixelCount = entry.ImageWidth * entry.ImageHeight;
                    iconStream.Seek(40, SeekOrigin.Current);
                    ////int rowSize = (bitsPerPixel * entry.ImageWidth) / 32 * 4;


                    var bmp = new Bitmap(entry.ImageWidth, entry.ImageHeight);
                    for (int pixelIndex = 0; pixelIndex < pixelCount; ++pixelIndex)
                    {
                        _SetPixel(pixelIndex);
                    }

                    _AddToFound(entry, bmp, eIcoImageType.Bmp);

                    void _SetPixel (int _pixel)
                    {
                        int y = (int)(_pixel / entry.ImageWidth);
                        int x = _pixel - (y * entry.ImageWidth);
                        y = entry.ImageHeight - (y + 1); // Invert the y since it's bottom up
                        bmp.SetPixel(x, y, System.Drawing.Color.FromArgb(reader.ReadInt32()));
                    }
                }
            }

            return found.ToArray();

            void _AddToFound (IcoEntryInfoBase _entry, Image _img, eIcoImageType _storeType)
            {
                found.Add(new IcoImagePart
                {
                    Image = ImageUtils.GetImageSourceFrom(_img),
                    ParsedInfo = _entry,
                    PartName = PartNameFor(_entry.ImageWidth,_entry.ImageHeight),
                    StoredImageType = _storeType,
                });
            }
        }

        public static IcoImagePart[] ReadImagePiecesFromIcon (string iconPath, out IcoHeader header)
        {
            using (FileStream stream = File.OpenRead(iconPath))
            {
                header = ReadIcoHeaderFrom(stream);
                return ReadImagePieces(header, stream);
            }
        }

        public static IcoImagePart[] ReadImagePiecesFromIcon (string iconPath)
        {
            return ReadImagePiecesFromIcon(iconPath, out var _);
        }

        public static void WriteIcon (string path, IcoHeader header, IEnumerable<IcoImagePart> imageParts)
        {
            using (FileStream stream = File.OpenWrite(path))
            {
                WriteIcon(stream, header, imageParts);
            }
        }

        public static void WriteIcon (Stream to, IcoHeader header, IEnumerable<IcoImagePart> imageParts)
        {
            IcoImagePart[] imagePartsArray = imageParts.ToArray();
            BinaryWriter writer = new BinaryWriter(to);
            writer.Write((ushort)0);
            writer.Write((ushort)(header.Type == eIcoType.IcoImages ? 1 : 0));
            writer.Write((ushort)imagePartsArray.Length);
            foreach (var part in imageParts)
            {
                _Write1To256(part.ParsedInfo.ImageWidth);
                _Write1To256(part.ParsedInfo.ImageHeight);
                writer.Write(part.ParsedInfo.ColorPaletteColorCount);
                writer.Write((byte)0);
                writer.Write(part.ParsedInfo.Offset4);
                writer.Write(part.ParsedInfo.Offset5);
                writer.Write(part.ParsedInfo.SizeInBytes);
                writer.Write(part.ParsedInfo.DataOffsetFromFileStart);
            }

            void _Write1To256 (ushort toByte)
            {
                writer.Write((byte)(toByte == 256 ? 0 : toByte));
            }
            
        }

        public static IcoImagePart ImagePartFor (string imagePath)
        {
            var img = ImageUtils.GetImageSourceFrom(imagePath);

            return new IcoImagePart
            {
                Image = img,
                ParsedInfo = new IconEntryInfo(img),
                PartName = PartNameFor((int)img.Width, (int)img.Height),
                StoredImageType = eIcoImageType.Png,
            };
        }

        private static string PartNameFor (int width, int height)
        {
            return $"{width}x{height}";
        }

    }

    public enum eIcoType { IcoImages, Cursor };
    public class IcoHeader
    {
        public eIcoType Type { get; set; }
        public ushort ImageCount { get; set; }
        public List<IcoEntryInfoBase> Entries { get; set; } = new List<IcoEntryInfoBase>();
    }

    public abstract class IcoEntryInfoBase
    {
        public ushort ImageWidth { get; set; }
        public ushort ImageHeight { get; set; }
        /// <summary>
        /// 0-255 where 0 means no color pallete used
        /// </summary>
        public byte ColorPaletteColorCount { get; set; } = 0;
        public bool IsColorPaletteUsed => this.ColorPaletteColorCount != 0;

        internal ushort Offset4 { get; set; }
        internal ushort Offset5 { get; set; }


        public uint SizeInBytes { get; set; }

        public uint DataOffsetFromFileStart { get; set; }

        public void ReadFrom (BinaryReader reader)
        {
            this.ImageWidth = _Read1To256();
            this.ImageHeight = _Read1To256();
            this.ColorPaletteColorCount = reader.ReadByte();
            reader.BaseStream.Seek(1, SeekOrigin.Current); // reserved

            this.Offset4 = reader.ReadUInt16();
            this.Offset5 = reader.ReadUInt16();

            this.SizeInBytes = reader.ReadUInt32();
            this.DataOffsetFromFileStart = reader.ReadUInt32();

            ushort _Read1To256 ()
            {
                byte b = reader.ReadByte();
                if (b == 0)
                {
                    return (ushort)256;
                }

                return (ushort)b;
            }

        }
    }

    public class IconEntryInfo : IcoEntryInfoBase
    {
        public IconEntryInfo ()
        {
        }

        public IconEntryInfo (BinaryReader reader)
        {
            this.Offset4 = 0;
            this.Offset5 = 32;
            this.ReadFrom(reader);
        }

        public IconEntryInfo (BitmapImage img)
        {
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(img));
            using MemoryStream finalBytes = new MemoryStream();
            png.Save(finalBytes);

            this.ImageWidth = (ushort)img.Width;
            this.ImageHeight = (ushort)img.Height;
            this.SizeInBytes = (uint)finalBytes.Length;
        }

        /// <summary>
        /// Either 0 or 1
        /// </summary>
        public ushort ColorPlanes => this.Offset4;

        public ushort BitsPerPixel => this.Offset5;
    }

    public class CursorEntryInfo : IcoEntryInfoBase
    {
        public CursorEntryInfo ()
        {
        }

        public CursorEntryInfo (BinaryReader reader)
        {
            this.ReadFrom(reader);
        }

        public ushort HorizontalHotspotCoords => this.Offset4;
        public ushort VerticalHotspotCoords => this.Offset5;
    }

    public enum eIcoImageType { Bmp, Png }
    public class IcoImagePart
    {
        public string PartName { get; set; }
        public eIcoImageType StoredImageType { get; set; }
        public ImageSource Image { get; set; }
        public IcoEntryInfoBase ParsedInfo { get; set; }
    }
}
