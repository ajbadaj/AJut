namespace AJut.Text
{
    using System;

    /// <summary>
    /// Very simple readability helpers (english)
    /// </summary>
    public static class ReadabilityHelpersEn
    {
        /// <summary>
        /// Create string with suffix for given number, thus 2 becomes "2nd", and so forth
        /// </summary>
        public static string NumberToSuffixedString(int number)
        {
            string stringNumber = number.ToString();
            switch (stringNumber[stringNumber.Length - 1])
            {
                case '1': return $"{stringNumber}st";
                case '2': return $"{stringNumber}nd";
                case '3': return $"{stringNumber}rd";
                default: return $"{stringNumber}th";
            }
        }

        /// <summary>
        /// Return the passed in string with a prefix of 'a' or 'an'
        /// </summary>
        public static string ProperlyPrefixAnOrA(string stringToPrefix, bool capitalize = false)
        {
            if (string.IsNullOrEmpty(stringToPrefix))
            {
                return String.Empty;
            }

            char resultFirst = capitalize ? 'A' : 'a';
            switch (char.ToLower(stringToPrefix.Trim()[0]))
            {
                case 'a':
                case 'e':
                case 'i':
                case 'o':
                case 'u': return $"{resultFirst}n {stringToPrefix}";
                default: return $"{resultFirst} {stringToPrefix}";
            }
        }
    }
}
