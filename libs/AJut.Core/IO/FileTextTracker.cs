namespace AJut.IO
{
    using AJut.Text;
    using System.IO;

    public class FileTextTracker : TrackedStringManager
    {
        public string Path { get; private set; }

        public FileTextTracker(string filePath) : base(File.ReadAllText(filePath))
        {
            this.Path = filePath;
        }

        /// <summary>
        /// Write back to the file this tracker represents (if any changes are present)
        /// </summary>
        public void WriteBack(OpenForWriteFunction fileOpener = null)
        {
            if (this.HasChanges)
            {
                fileOpener = fileOpener ?? File.OpenWrite;
                using (FileStream stream = fileOpener(this.Path))
                {
                    if (stream != null)
                    {
                        StreamWriter writer = new StreamWriter(stream);
                        writer.Write(this.Text);
                    }
                }
            }
        }
    }
}
