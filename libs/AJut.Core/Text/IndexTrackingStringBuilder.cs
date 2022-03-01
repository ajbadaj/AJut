namespace AJut.Text
{
    using System;
    using System.Text;

    public class IndexTrackingStringBuilder
    {
        public int NextWriteIndex { get; private set; }

        public StringBuilder Builder { get; private set; }

        public int StartIndex { get; private set; }

        public IndexTrackingStringBuilder(int startIndex = 0)
        {
            this.StartIndex = startIndex;
            this.NextWriteIndex = startIndex;
            this.Builder = new StringBuilder();
        }

        public void Write(string format, params object[] formatArgs)
        {
            string result;
            if (formatArgs.IsNullOrEmpty())
            {
                result = format;
            }
            else
            {
                result = String.Format(format, formatArgs);
            }

            this.Builder.Append(result);
            this.NextWriteIndex += result.Length;
        }
    }
}
