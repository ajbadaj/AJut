namespace AJut.IO
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;

    public class FileTextBuilder
    {
        public string Newline { get; set; }
        public string Tabbing { get; set; }

        StringBuilder m_builder = new StringBuilder();

        ushort m_currentTabOffset;

        public FileTextBuilder(string newline = "\r\n", string tabbing = "\t")
        {
            this.Newline = newline;
            this.Tabbing = tabbing;
        }

        public void Write(string format, params object[] formatArgs)
        {
            m_builder.AppendFormat(format, formatArgs);
        }

        public void WriteNewline(bool addTabbing = true)
        {
            this.Write(this.Newline);
            if(addTabbing)
            {
                this.WriteTabbing();
            }
        }

        public void WriteTabbing()
        {
            for (int numTabsRemaining = m_currentTabOffset; numTabsRemaining > 0; --numTabsRemaining)
            {
                this.Write(this.Tabbing);
            }
        }

        public void IncreaseTabbing()
        {
            ++m_currentTabOffset;
        }

        public void DecreaseTabbing()
        {
            if (m_currentTabOffset != 0)
            {
                --m_currentTabOffset;
            }
        }

        // Thought I'd obfuscate this incase I ever move away from StringBuilder,
        //  and because I wanted something more explicit.

        /// <summary>
        /// Compiles all the output text for the file.
        /// </summary>
        /// <returns>The compiled output text</returns>
        public string CompileText()
        {
            return m_builder.ToString();
        }

        public bool OutputTo(Stream stream, int offset = 0, Encoding encoding = null)
        {
            try
            {
                encoding ??= Encoding.UTF8;
                var outputBytes = encoding.GetBytes(this.CompileText());
                stream.Write(outputBytes, offset, outputBytes.Length);
                return true;
            }
            catch(Exception exc)
            {
                Logger.LogError(exc);
                return false;
            }
        }

        public bool OutputToFile(string filePath, bool forceWritable = false)
        {
            FileStream stream;
            if (FileHelpers.TryOpenForWrite(filePath, forceWritable, out stream))
            {
                this.OutputTo(stream);
            }

            return false;
        }

        public override string ToString()
        {
            return CompileText();
        }
    }
}
