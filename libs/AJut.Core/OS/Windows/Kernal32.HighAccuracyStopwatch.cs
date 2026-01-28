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
                Kernal32DllImports.QueryPerformanceFrequency(out m_frequency);
                this.Reset();
            }
            public void Reset ()
            {
                Kernal32DllImports.QueryPerformanceCounter(out m_startTime);
            }

            public TimeSpan GetElapsed ()
            {
                Kernal32DllImports.QueryPerformanceCounter(out long endTime);
                return TimeSpan.FromSeconds((double)(endTime - m_startTime) / m_frequency);
            }

            public TimeSpan MeasureAndReset()
            {
                Kernal32DllImports.QueryPerformanceCounter(out long endTime);
                TimeSpan found = TimeSpan.FromSeconds((double)(endTime - m_startTime) / m_frequency);
                m_startTime = endTime;
                return found;
            }
        }

    }
}
