namespace AJut.Text
{
    using System;

    public static class ReadabilityHelpers
    {
        /// <summary>
        /// 2 becomes "2nd", and so forth
        /// </summary>
        /// <param name="nVal">The number value to evaluate</param>
        /// <returns>Suffixed string</returns>
        public static string NumberToSuffixedString(int number)
        {
            string stringNumber = number.ToString();
            char lastLetter = stringNumber[stringNumber.Length - 1];

            switch (lastLetter)
            {
                case '0': { stringNumber += "th"; } break;
                case '1': { stringNumber += "st"; } break;
                case '2': { stringNumber += "nd"; } break;
                case '3': { stringNumber += "rd"; } break;
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9': { stringNumber += "th"; } break;
            }

            return stringNumber;
        }

        public static string ProperlyPrefixAnOrA(string stringToPrefix, bool capitalize = false)
        {
            if (string.IsNullOrEmpty(stringToPrefix))
            {
                return String.Empty;
            }

            string loweredString = stringToPrefix.ToLower();
            switch (loweredString[0])
            {
                case 'a':
                case 'e':
                case 'i':
                case 'o':
                case 'u': { stringToPrefix = (capitalize ? "An " : "an ") + stringToPrefix; } break;
                default: { stringToPrefix = (capitalize ? "A " : "a ") + stringToPrefix; } break;
            }

            return stringToPrefix;
        }
    }
}
