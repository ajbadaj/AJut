namespace AJut.OS.Windows
{
    using System;

    /// <summary>
    /// Kernal32.dll based utilities
    /// </summary>
    public static partial class Kernal32
    {
        /// <summary>
        /// Uses the OS performance counter to create a high resolution timer with an error margin of &lt;1us.
        /// </summary>
        public class HighAccuracyStopwatch
        {
            private long m_frequency;
            private long m_startTime;

            public HighAccuracyStopwatch ()
            {
                DllImport.QueryPerformanceFrequency(out m_frequency);
            }
            public void Start ()
            {
                DllImport.QueryPerformanceCounter(out m_startTime);
            }

            public TimeSpan Stop ()
            {
                DllImport.QueryPerformanceCounter(out long endTime);
                return TimeSpan.FromSeconds((double)(endTime - m_startTime) / (double)m_frequency);
            }
        }

    }
}
