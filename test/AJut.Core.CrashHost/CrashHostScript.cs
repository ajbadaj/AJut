namespace AJut.Core.CrashHost
{
    /// <summary>
    /// The ways the crash host can die. Each one ends the process without any of the normal shutdown running.
    /// </summary>
    public enum eCrashHostDeath
    {
        /// <summary><see cref="System.Environment.FailFast(string)"/> - no handlers, no finalizers, gone.</summary>
        FailFast,

        /// <summary>Unbounded recursion. The runtime can't recover from a stack overflow, so it takes the process down.</summary>
        StackOverflow,

        /// <summary>An exception nobody catches, thrown on a thread of its own.</summary>
        UnhandledException,

        /// <summary>A native thread jumping to an address that isn't there - a crash with no managed code in the way.</summary>
        AccessViolation,
    }

    /// <summary>
    /// What the crash host logs before it dies, shared with the tests so they know what to count.
    /// </summary>
    public static class CrashHostScript
    {
        public const int kBurstLineCount = 2000;
        public const int kTrailingLineCount = 200;
        public const int kTrailingLineGapMs = 1;

        public const string kBurstTag = "[BURST]";
        public const string kErrorTag = "[ERROR-LINE]";
        public const string kTrailingTag = "[TRAIL]";
        public const string kLastTag = "[LAST-LINE]";
    }
}
