namespace AJut.OS.Run
{
    using System;

    /// <summary>
    /// Event argument wrapping the text that comes out of a process and is captured by a <see cref="ProcRunner"/>
    /// </summary>
    public class ProcessOutputReceivedEventArgs : EventArgs
    {
        public eProcOutputType OutputType { get; private set; }
        public string OutputText { get; private set; }

        public static ProcessOutputReceivedEventArgs Output (string text)
        {
            return new ProcessOutputReceivedEventArgs
            {
                OutputType = eProcOutputType.Output,
                OutputText = text
            };
        }

        public static ProcessOutputReceivedEventArgs Error (string text)
        {
            return new ProcessOutputReceivedEventArgs
            {
                OutputType = eProcOutputType.Error,
                OutputText = text
            };
        }
    }

}
