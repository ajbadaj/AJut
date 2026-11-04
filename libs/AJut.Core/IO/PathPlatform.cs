namespace AJut.IO
{
    using System;

    /// <summary>
    /// The path rules of the platforms AJut runs on, as far as comparing and validating a path goes
    /// </summary>
    internal enum ePathPlatform
    {
        /// <summary>Case is ignored, '\' and '/' both separate, and a path's length is limited (MAX_PATH)</summary>
        Windows,

        /// <summary>
        /// Case is ignored, only '/' separates ('\' is part of a name), and a path's length is not limited. Mac Catalyst
        /// counts as macOS.
        /// </summary>
        MacOS,

        /// <summary>
        /// Android, iOS, Linux and any other platform: case tells paths apart, only '/' separates ('\' is part of a name), and
        /// a path's length is not limited
        /// </summary>
        CaseSensitive,
    }

    // Which platform's path rules PathHelpers follows. Each platform's own file system sets them by default: NTFS on Windows
    //  and APFS on macOS ignore case, so 'Photos' and 'photos' are one folder there, while ext4 on Linux and Android, and
    //  APFS on iOS, tell the two apart. Only Windows limits a path's length, to MAX_PATH. A volume can still differ from its
    //  platform: a case-sensitive APFS volume on a Mac, or Android's shared storage or a FAT card, both of which ignore case.
    //  That is why DirectoryHelpers asks the volume itself before a move or copy that could merge a folder onto itself.
    //
    //  Windows takes '/' as a separator as well as '\'. Everywhere else only '/' is one, and '\' is an ordinary character in
    //  a name, so on Linux a\b is one file, not b in a folder named a.
    //
    //  Mac Catalyst runs on the Mac's own file system, though OperatingSystem.IsIOS is true there too, so it is checked
    //  before falling through to the case-sensitive rule.
    //
    //  The test runner is only ever one platform, so tests stand in for the others with Simulate, on their own thread only,
    //  the same way DynamicCodeAvailability does.
    /// <summary>
    /// Which platform's path rules apply here: whether case tells two paths apart, and whether a path's length is limited
    /// </summary>
    internal static class PathPlatform
    {
        private static readonly ePathPlatform kRunningOn = DetermineRunningPlatform();
        private static readonly char[] kWindowsSeparators = { '\\', '/' };
        private static readonly char[] kSeparators = { '/' };

        [ThreadStatic]
        private static ePathPlatform? g_simulated;

        // ===========[ Properties ]============================================
        /// <summary>The platform whose path rules apply here</summary>
        public static ePathPlatform Current => g_simulated ?? kRunningOn;

        /// <summary>Whether two paths that differ only by case name the same place here</summary>
        public static bool IgnoresCase => Current != ePathPlatform.CaseSensitive;

        /// <summary>Whether a path's length is limited here, to <see cref="PathHelpers.kMaxFullPathLength"/> and <see cref="PathHelpers.kMaxPathDirectoryLength"/></summary>
        public static bool LimitsPathLength => Current == ePathPlatform.Windows;

        /// <summary>How path text compares here: ordinal, ignoring case where the platform does</summary>
        public static StringComparison Comparison => IgnoresCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        /// <summary>Whether '\' separates a path here, as well as '/': on Windows only. Elsewhere '\' is part of a name.</summary>
        public static bool TakesBackslashAsSeparator => Current == ePathPlatform.Windows;

        /// <summary>The one separator a path is put back together with here: '\' on Windows, '/' elsewhere</summary>
        public static char NormalizedSeparator => TakesBackslashAsSeparator ? '\\' : '/';

        /// <summary>Every character that separates a path here. Shared, so never changed by a caller.</summary>
        public static char[] Separators => TakesBackslashAsSeparator ? kWindowsSeparators : kSeparators;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Whether <paramref name="character"/> separates a path here</summary>
        public static bool IsSeparator (char character) => (character == '/') || ((character == '\\') && TakesBackslashAsSeparator);

        /// <summary>For tests: this thread follows <paramref name="platform"/>'s path rules until the result is disposed</summary>
        public static IDisposable Simulate (ePathPlatform platform)
        {
            var scope = new SimulationScope(g_simulated);
            g_simulated = platform;
            return scope;
        }

        // ===========[ Helper Methods ]========================================
        private static ePathPlatform DetermineRunningPlatform ()
        {
            if (OperatingSystem.IsWindows())
            {
                return ePathPlatform.Windows;
            }

            if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
            {
                return ePathPlatform.MacOS;
            }

            return ePathPlatform.CaseSensitive;
        }

        // ===========[ Subclasses/structs ]====================================
        private sealed class SimulationScope : IDisposable
        {
            private readonly ePathPlatform? m_wasSimulating;

            public SimulationScope (ePathPlatform? wasSimulating)
            {
                m_wasSimulating = wasSimulating;
            }

            public void Dispose ()
            {
                g_simulated = m_wasSimulating;
            }
        }
    }
}
