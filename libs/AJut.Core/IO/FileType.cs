namespace AJut.IO
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    public class FileType
    {
        private static readonly List<FileType> g_allRegisteredTypes = new List<FileType>();

        // =============================[ Construction ]===================================
        static FileType ()
        {
            AllRegisteredFileTypes = g_allRegisteredTypes.AsReadOnly();
        }

        public FileType (string name, params string[] extensions)
        {
            this.Name = name;
            this.Extensions = extensions;

            this.FileFilter = $"{this.Name} ({"*." + string.Join("; *.", extensions)})|{"*." + string.Join(";*.", extensions)}";
            g_allRegisteredTypes.Add(this);
        }

        // =============================[ Properties ]======================================
        public static IReadOnlyCollection<FileType> AllRegisteredFileTypes { get; }

        public string Name { get; }
        public string[] Extensions { get; }
        public string MainExtension => this.Extensions[0];
        public string FileFilter { get; }

        // =============================[ Methods ]=========================================
        public bool MatchesFileExtension (string path)
        {
            return this.DoExtensionMatchingWithProperString(Path.GetExtension(path).TrimStart('.'));
        }

        public bool MatchesExtension (string extension)
        {
            if (extension.StartsWith('.'))
            {
                extension = extension.TrimStart('.');
            }

            return this.DoExtensionMatchingWithProperString(extension);
        }

        private bool DoExtensionMatchingWithProperString (string extension)
        {
            return this.Extensions.Any(xt => xt.Equals(extension, StringComparison.InvariantCultureIgnoreCase));
        }

        // =============================[ Static Utility Interface ]=========================
        public static IEnumerable<FileType> FindByPathMatch (string path)
        {
            string pathxt = Path.GetExtension(path).Trim('.');
            return g_allRegisteredTypes.Where(t => t.MatchesExtension(pathxt));
        }
    }
}
