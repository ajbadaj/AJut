namespace AJut.Application
{
    using AJut.IO;

    public static class ImageTypes
    {
        public static FileType Bitmap { get; } = new FileType("Bitmap Image", "bmp", "dib", "rle");
        public static FileType Gif { get; } = new FileType("Gif Image", "gif");
        public static FileType Jpg { get; } = new FileType("JPEG Image", "jpg", "jpeg", "jpe", "jiff", "exif");
        public static FileType JpgXr { get; } = new FileType("JPEG Image", "jxr", "wdp", "wmp");
        public static FileType Tiff { get; } = new FileType("Tiff Image", "tiff", "tif");
        public static FileType Png { get; } = new FileType("Png Image", "png");
        public static FileType Heic { get; } = new FileType("High Efficiency Image", "heic", "heif");
        public static FileType WebP { get; } = new FileType("WebP", "webp");
        public static FileType AVIF { get; } = new FileType("AVIF", "avif");
        public static FileType Icon { get; } = new FileType("Ico", "ico");
        public static FileType Cursor { get; } = new FileType("Cursor", "cur");

        public static FileType[] AllImageTypes { get; } = new FileType[] { Bitmap, Gif, Jpg, JpgXr, Tiff, Png, Heic, WebP, AVIF, Icon, Cursor };
        public static string AllImageTypesFilter { get; } = PathHelpers.CreateFileFilterFor(AllImageTypes);
        public static string AnyImageTypeFilter { get; } = PathHelpers.CreateGroupFilter("Any Image", AllImageTypes);
        public static string AnyOrAllImageTypeFilter { get; } = $"{AnyImageTypeFilter}|{AllImageTypesFilter}";
    }
}
