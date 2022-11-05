namespace AJut.Security
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Storage for encrypted obfuscated strings, stores both the basic string, and the x64 compiled version and provides whichever is active.
    /// </summary>
    [DebuggerDisplay("ObfuscatedString - hidden from the debugger")]
    public class ObfuscatedString
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly string m_str;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly string m_x64Str;

        public ObfuscatedString (string str, string x64str)
        {
            m_str = str;
            m_x64Str = x64str;
        }

        /// <summary>
        /// The architecture target determined, encrypted, string to utilize
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public string Active => Environment.Is64BitProcess ? m_x64Str : m_str;
    }
}
