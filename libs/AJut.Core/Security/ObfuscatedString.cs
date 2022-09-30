namespace AJut.Security
{
    using System;

    /// <summary>
    /// Storage for encrypted obfuscated strings, stores both the basic string, and the x64 compiled version and provides whichever is active.
    /// </summary>
    public class ObfuscatedString
    {
        private readonly string m_str;
        private readonly string m_x64Str;

        public ObfuscatedString (string str, string x64str)
        {
            m_str = str;
            m_x64Str = x64str;
        }

        /// <summary>
        /// The architecture target determined, encrypted, string to utilize
        /// </summary>
        public string Active => Environment.Is64BitProcess ? m_x64Str : m_str;
    }
}
