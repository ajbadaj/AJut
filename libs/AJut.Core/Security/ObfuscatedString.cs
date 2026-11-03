namespace AJut.Security
{
    using System.Diagnostics;

    /// <summary>
    /// Storage for an encrypted obfuscated string. Obfuscation is bitness-independent now, so a single
    /// encrypted value is stored and handed back regardless of whether the process is x86 or x64.
    /// </summary>
    [DebuggerDisplay("ObfuscatedString - hidden from the debugger")]
    public class ObfuscatedString
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly string m_str;

        public ObfuscatedString (string value)
        {
            m_str = value;
        }

        /// <summary>
        /// The encrypted string to utilize
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public string Active => m_str;
    }
}
