namespace AJut
{
    /// <summary>
    /// How far each log line is pushed before the log call returns. Set on <see cref="Logger.FlushMode"/>.
    /// </summary>
    public enum eLogFlushMode
    {
        /// <summary>
        /// Lines collect in the logger's in-process buffer and go out when it fills, or on <see cref="Logger.ForceFlushToFile"/>.
        /// Cheapest, but whatever is still buffered is lost if the process dies.
        /// </summary>
        Buffered,

        /// <summary>
        /// Each line is handed to the OS before the log call returns. It survives the process dying by any means (a crash, a
        /// fail fast, being killed) but not the machine going down (power loss, bluescreen). Costs microseconds a line.
        /// </summary>
        FlushToOS,

        /// <summary>
        /// Each line is forced all the way to the physical disk before the log call returns, so it survives even the machine
        /// going down. That means waiting on the drive for every line - around a quarter millisecond on a fast SSD, far longer
        /// on a slow or busy one - and every other thread that logs waits behind it.
        /// </summary>
        FlushToDisk,
    }
}
