namespace AJut
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    public enum eStringShortening { TakeFromMiddle, TakeFromEnd };
    public enum eCompletionEnd { Beginning, End }

    public static class StringXT
    {
        public static readonly string[] kUpperCaseLetters = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" };
        public static readonly char[] kSymbols = { ' ', '~', '`', '!', '@', '#', '$', '%', '^', '&', '*', '(', ')', '_', '-', '+', '=', '{', '}', '[', ']', '|', '\\', ':', ';', '\'', '\"', '<', '>', ',', '.', '?', '/' };
        
        // Does an index based substirng
        public static string SubstringWithIndices(this string source, int startIndex, int endIndex)
        {
            return source.Substring(startIndex, endIndex - startIndex + 1);
        }

        public static string Replace(this string source, int startIndex, int endIndex, string newValue)
        {
            StringBuilder output = new StringBuilder(source.Substring(0, startIndex));
            output.Append(newValue);

            if (source.Length - 1 > endIndex)
            {
                output.Append(source.Substring(endIndex + 1));
            }

            return output.ToString();
        }

        public static string Replace(this string source, Capture regexCapture, string newValue)
        {
            if(regexCapture == null)
            {
                return source;
            }

            return source.Replace(regexCapture.Index, regexCapture.Index + regexCapture.Length - 1, newValue);
        }

        /// <summary>
        /// Will find the complete range between the first index of the opener char and the completing
        /// index of the closing char. If there are other opener/closer combos inbetween, those will be
        /// skipped over.
        /// </summary>
        /// <param name="searchStartIndex">Where to start looking</param>
        /// <param name="opener">The char that begins the sequence whose range we are looking to find.</param>
        /// <param name="closer">The char that ends the sequence whose range we are looking to find.</param>
        /// <returns>The range found, or null if incomplete</returns>
        public static Tuple<int, int> FindIndexRange(this string source, int searchStartIndex, char opener, char closer)
        {
            int currentSearchPosition = searchStartIndex;

            int startOpen = source.IndexOf(opener, currentSearchPosition);

            if (startOpen == -1)
            {
                return null;
            }

            int nextOpen = startOpen;
            int nextClose = source.IndexOf(closer, currentSearchPosition);
            int numOpens = 0;
            while (true)
            {
                if (nextClose == -1)
                {
                    return null;
                }

                if (nextOpen != -1 && nextOpen < nextClose)
                {
                    ++numOpens;
                    currentSearchPosition = nextOpen + 1;
                    nextOpen = source.IndexOf(opener, currentSearchPosition);
                }
                else
                {
                    if (--numOpens == 0)
                    {
                        return new Tuple<int, int>(startOpen, nextClose);
                    }

                    currentSearchPosition = nextClose + 1;
                    nextClose = source.IndexOf(closer, currentSearchPosition);
                }
            }
        }

        public static bool IsNullOrEmpty(this string source)
        {
            return source == null || source.Length == 0;
        }

        /// <summary>
        /// Generate a stable integer hashcode (will persist between runs, though will differ between 32bit and 64bit process runs) for a string - this does not make any security considerations!
        /// </summary>
        /// <param name="source">The source string</param>
        /// <returns>A stable hashcode integer of the string</returns>
        /// <remarks>
        /// Adapted from dotnet source, found here: https://referencesource.microsoft.com/#mscorlib/system/string.cs,0a17bbac4851d0d4
        /// </remarks>
        public static int GenerateStableHashCode (this string source)
        {
            int hash1;
            if (Environment.Is64BitProcess)
            {
                hash1 = (5381 << 16) + 5381;
            }
            else
            {
                hash1 = 5381;
            }
            int hash2 = hash1;

            for (int index = 0; index < source.Length && source[index] != '\0'; index += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ source[index];
                if (index == source.Length - 1 || source[index + 1] == '\0')
                {
                    break;
                }


                hash2 = ((hash2 << 5) + hash2) ^ source[index + 1];
            }

            return hash1 + (hash2 * 1566083941);
        }


        /// <summary>
        /// Best extension ever!   "{0} Sweet".ApplyFormatArgs("source is");
        /// Also takes proper safety precautions
        /// </summary>
        public static string ApplyFormatArgs(this String source, params object[] formatArgs)
        {
            if(source == null)
            {
                return null;
            }

            if(formatArgs.IsNullOrEmpty())
            {
                return source;
            }

            return String.Format(source, formatArgs);
        }

        public static string Shorten(this string source, int maxLength, eStringShortening shortenWhere)
        {
            return StringShortenerWorkhorse(source, maxLength, false, shortenWhere, "...");
        }
        public static string Shorten(this string source, int maxLength, eStringShortening shortenWhere, string removedCharactersIndicator)
        {
            return StringShortenerWorkhorse(source, maxLength, false, shortenWhere, removedCharactersIndicator);
        }

        public static bool StartsWithAny(this string source, params string[] strings)
        {
            if (source == null || strings.IsNullOrEmpty())
                return false;

            foreach (string s in strings)
            {
                if (source.StartsWith(s))
                    return true;
            }
            return false;
        }

        public static string Overwrite(this string source, int nStart, int nEnd, string sNewText)
        {
            return source.Remove(nStart, nEnd - nStart).Insert(nStart, sNewText);
        }

        public static int IndexAfter(this string source, string test)
        {
            int nInd = source.IndexOf(test);
            if (nInd == -1)
                return -1;
            return nInd + test.Length;
        }
        public static int IndexAfter(this string source, string test, int startIndex)
        {
            int nInd = source.IndexOf(test, startIndex);
            if (nInd == -1)
                return -1;
            return nInd + test.Length;
        }

        /// <summary>
        /// Converts the string to a friendly version ("The_fatDog" becomes "The Fat Dog")
        /// </summary>
        /// <param name="source">The string to convert</param>
        /// <returns>A friendly version of the string</returns>
        public static string ConvertToFriendlyEn(this String source)
        {
            String result = source.Replace("_", " ").DistinctifyClumps(" ");

            foreach (string upperLetter in kUpperCaseLetters)
            {
                result = result.Replace(upperLetter, " " + upperLetter);
            }

            return result.Trim();
        }

        public static IEnumerable<char> FindCapitalsEn (this string source)
        {
            foreach (char letter in source)
            {
                if (letter >= 'A' && letter <= 'Z')
                {
                    yield return letter;
                }
            }
        }

        public static string ToCombinedString (this IEnumerable<char> characters)
        {
            return String.Join(String.Empty, characters.Select(c => c.ToString()));
        }

        /// <summary>
        /// Removes any clumps of repeating values matching the passed in value. Thus "Hello--dude".DistinctifyClumps("-") == "Hello-dude"
        /// </summary>
        /// <param name="source">The string being this is being called on</param>
        /// <param name="sString">The string to remove clumps of</param>
        /// <returns>A version of this string that only contains distinct versions of the passed in string</returns>
        public static string DistinctifyClumps(this String source, string sString)
        {
            string[] splitResults = source.Split(new string[] { sString }, StringSplitOptions.RemoveEmptyEntries);
            string result = "";
            for (int nIndex = 0; nIndex < splitResults.Length; ++nIndex)
            {
                result += splitResults[nIndex] + (nIndex == splitResults.Length - 1 ? "" : sString);
            }

            return result;
        }

        /// <summary>
        /// Get the reverse version of the string
        /// </summary>
        /// <param name="source">The string being operated on</param>
        /// <returns>The reverse version of the string</returns>
        public static string Reverse(this string source)
        {
            char[] arr = source.ToCharArray();
            Array.Reverse(arr);
            return new string(arr);
        }

        /// <summary>
        /// Returns a string that is represents the string between (and including) the passed in indices.
        /// </summary>
        /// <param name="source">The string to evaluate</param>
        /// <param name="startInd">The index that starts the new string</param>
        /// <param name="endInd">The last index of the new string</param>
        /// <returns>The string located between the passed in indices</returns>
        public static string SubstringByInd(this string source, int startInd, int endInd)
        {
            return source.Substring(startInd, endInd - startInd + 1);
        }

        /// <summary>
        /// Gets the substring located between (and including) the start index, and the relative position from the end of the string. Thus if your 
        /// string stored 'The dog' and you passed in a start index of 1 and a relative end distance of 1, the resulting substring would be 'he do'.
        /// </summary>
        /// <param name="source">The string to evaluate</param>
        /// <param name="startInd">The index of to start the substring (substring includes character at startIndex)</param>
        /// <param name="relativeDistanceFromEndInd">The distance from the end of the string to end the substring (substring includes the character indicated by this parameter)</param>
        /// <returns>The substring located between (and including) the start index, and the relative position from the end of the string.</returns>
        public static string SubstringFromRelativeEnd(this string source, int startInd, int relativeDistanceFromEndInd)
        {
            return source.SubstringByInd(startInd, source.Length - relativeDistanceFromEndInd - 1);
        }

        /// <summary>
        /// Reports the index of the first occurrence of the specified test string in this instance.
        /// </summary>
        /// <param name="source">The string to evaluate</param>
        /// <param name="test">The test string to find</param>
        /// <param name="startIndex">The index of the start of the range to look through</param>
        /// <param name="endIndex">The index of the end of the range to look through</param>
        /// <returns>The index of the first occurrence of the specified test string in this instance, or if not found, -1</returns>
        public static int IndexOfByInd(this string source, string test, int startIndex, int endIndex)
        {
            return source.IndexOf(test, startIndex, endIndex - startIndex + 1);
        }

        /// <summary>
        /// Reports the index of the first occurrence of the specified test string in this instance.
        /// </summary>
        /// <param name="source">The string to evaluate</param>
        /// <param name="test">The test string to find</param>
        /// <param name="startIndex">The index of the start of the range to look through</param>
        /// <param name="endIndex">The index of the end of the range to look through</param>
        /// <param name="comparison">One of the System.StringComparison values indicating desired string comparison type</param>
        /// <returns>The index of the first occurrence of the specified test string in this instance, or if not found, -1</returns>
        public static int IndexOfByInd(this string source, string test, int startIndex, int endIndex, StringComparison comparison)
        {
            return source.IndexOf(test, startIndex, endIndex - startIndex + 1, comparison);
        }

        /// <summary>
        /// Reports the index of the first occurrence of the specified test string in this instance (searching back to front).
        /// </summary>
        /// <param name="source">The string to evaluate</param>
        /// <param name="test">The test to find</param>
        /// <returns>The index of the first occurrence of the specified test string in this instance (while searching back to front), or if not found, -1</returns>
        public static int ReverseIndexOf(this string source, string test)
        {
            int nResult = source.Reverse().IndexOf(test);
            if (nResult == -1)
                return -1;
            return source.Length - nResult - 1;
        }

        /// <summary>
        /// Reports the index of the first occurrence of the specified test string in this instance (searching back to front).
        /// </summary>
        /// <param name="source">The string to evaluate</param>
        /// <param name="test">The test to find</param>
        /// <param name="comparison">One of the System.StringComparison values indicating desired string comparison type</param>
        /// <returns>The index of the first occurrence of the specified test string in this instance (while searching back to front), or if not found, -1</returns>
        public static int ReverseIndexOf(this string source, string test, StringComparison comparison)
        {
            int nResult = source.Reverse().IndexOf(test, comparison);
            if (nResult == -1)
                return -1;
            return source.Length - nResult - 1;
        }

        /// <summary>
        /// Reports the index of the first occurrence of the specified test string in this instance (searching back to front).
        /// </summary>
        /// <param name="source">The string to evaluate</param>
        /// <param name="test">The test to find</param>
        /// <param name="nStart">The reverse searching start position</param>
        /// <returns>The index of the first occurrence of the specified test string in this instance (while searching back to front), or if not found, -1</returns>
        public static int ReverseIndexOf(this string source, string test, int nStart)
        {
            int nResult = source.Reverse().IndexOf(test, source.Length - nStart);
            if (nResult == -1)
                return -1;
            return source.Length - nResult - 1;
        }

        /// <summary>
        /// Reports the index of the first occurrence of the specified test string in this instance (searching back to front).
        /// </summary>
        /// <param name="source">The string to evaluate</param>
        /// <param name="test">The test to find</param>
        /// <param name="nStart">The reverse searching start position</param>
        /// <param name="comparison">One of the System.StringComparison values indicating desired string comparison type</param>
        /// <returns>The index of the first occurrence of the specified test string in this instance (while searching back to front), or if not found, -1</returns>
        public static int ReverseIndexOf(this string source, string test, int nStart, StringComparison comparison)
        {
            int nResult = source.Reverse().IndexOf(test, source.Length - nStart, comparison);
            if (nResult == -1)
                return -1;
            return source.Length - nResult - 1;
        }

        /// <summary>
        /// Reports the index of the first occurrence of the specified test string in this instance (searching back to front).
        /// </summary>
        /// <param name="source">The string to evaluate</param>
        /// <param name="test">The test to find</param>
        /// <param name="nStart">The reverse searching start position</param>
        /// <param name="nCount">The max number of characters to search through</param>
        /// <returns>The index of the first occurrence of the specified test string in this instance (while searching back to front), or if not found, -1</returns>
        public static int ReverseIndexOf(this string source, string test, int nStart, int nCount)
        {
            int nResult = source.Reverse().IndexOf(test, source.Length - nStart, nCount);
            if (nResult == -1)
                return -1;
            return source.Length - nResult - 1;
        }

        /// <summary>
        /// Reports the index of the first occurrence of the specified test string in this instance (searching back to front).
        /// </summary>
        /// <param name="source">The string to evaluate</param>
        /// <param name="test">The test to find</param>
        /// <param name="nStart">The reverse searching start position</param>
        /// <param name="nCount">The max number of characters to search through</param>
        /// <param name="comparison">One of the System.StringComparison values indicating desired string comparison type</param>
        /// <returns>The index of the first occurrence of the specified test string in this instance (while searching back to front), or if not found, -1</returns>
        public static int ReverseIndexOf(this string source, string test, int nStart, int nCount, StringComparison comparison)
        {
            int nResult = source.Reverse().IndexOf(test, source.Length - nStart, nCount, comparison);
            if (nResult == -1)
                return -1;
            return source.Length - nResult - 1;
        }

        /// <summary>
        /// Reports the index of the first occurrence (searching back to front) of the specified test string in this instance
        /// </summary>
        /// <param name="source">The string to evaluate</param>
        /// <param name="test">The test string to find</param>
        /// <param name="startIndex">The index of the start of the range to look through</param>
        /// <param name="endIndex">The index of the end of the range to look through</param>
        /// <returns>The index of the first occurrence of the specified test string in this instance (while searching back to front), or if not found, -1</returns>
        public static int ReverseIndexOfByInd(this string source, string test, int startIndex, int endIndex)
        {
            int nResult = source.Reverse().IndexOfByInd(test, startIndex, endIndex);
            if (nResult == -1)
                return -1;
            return source.Length - nResult - 1;
        }

        /// <summary>
        /// Reports the index of the first occurrence of the specified test string in this instance.
        /// </summary>
        /// <param name="source">The string to evaluate</param>
        /// <param name="test">The test string to find</param>
        /// <param name="startIndex">The index of the start of the range to look through</param>
        /// <param name="endIndex">The index of the end of the range to look through</param>
        /// <param name="comparison">One of the System.StringComparison values indicating desired string comparison type</param>
        /// <returns>The index of the first occurrence of the specified test string in this instance, or if not found, -1</returns>
        public static int ReverseIndexOfByInd(this string source, string test, int startIndex, int endIndex, StringComparison comparison)
        {
            int nResult = source.Reverse().IndexOfByInd(test, startIndex, endIndex, comparison);
            if (nResult == -1)
                return -1;
            return source.Length - nResult - 1;
        }

        /// <summary>
        /// Deletes a specified number of characters from this instance beginning at a specified index, and ending at a specified index.
        /// </summary>
        /// <param name="source">The string to evaluate</param>
        /// <param name="startIndex">The index of the first character of the range to remove</param>
        /// <param name="endIndex">The index of the last character of the range to remove</param>
        /// <returns>The string with the characters in the specified range removed</returns>
        public static string RemoveByInds(this string source, int startIndex, int endIndex)
        {
            return source.Remove(startIndex, endIndex - startIndex + 1);
        }

        /// <summary>
        /// Returns the number of unique instances of the test string located in the source string
        /// </summary>
        /// <param name="source">The string to evaluate</param>
        /// <param name="test">The test string to look for</param>
        /// <returns>The number of unique instances of the test string located in the source string</returns>
        public static int NumberOfTimesContained(this string source, string test)
        {
            int testIndex = 0;
            int counter = 0;
            while ((testIndex = source.IndexOf(test, testIndex)) != -1)
            {
                ++counter;
                ++testIndex;
            }
            return counter;
        }

        /// <summary>
        /// Returns the number of unique instances of the test string located in this string between after the indicated start index
        /// </summary>
        /// <param name="source">The string to evaluate</param>
        /// <param name="test">The test string to look for</param>
        /// <param name="startIndex">The index at which to start looking for the test string</param>
        /// <returns>The number of unique instances of the test string located in this string between after the indicated start index</returns>
        public static int NumberOfTimesContained(this string source, string test, int startIndex)
        {
            return source.Substring(startIndex, source.Length - startIndex).NumberOfTimesContained(test);
        }

        /// <summary>
        /// Returns the number of unique instances of the test string located in this string between the start and end indicies
        /// </summary>
        /// <param name="source">The string to evaluate</param>
        /// <param name="test">The test string to look for</param>
        /// <param name="startIndex">The index of the start of the range to look through</param>
        /// <param name="endIndex">The index of the end of the range to look through</param>
        /// <returns>The number of unique instances of the test string located in this string between the start and end indicies</returns>
        public static int NumberOfTimesContained(this string source, string test, int startIndex, int endIndex)
        {
            return source.SubstringByInd(startIndex, endIndex).NumberOfTimesContained(test);
        }

        /// <summary>
        /// Retrieves the missing part of the test string needed to complete this string.
        /// </summary>
        /// <param name="this_StringToComplete">The string to complete</param>
        /// <param name="in_TestString">The string that might complete it</param>
        /// <param name="completionEnd">For partial matches. The end of the string to check for a missing portion of the string to complete.</param>
        /// <remarks>
        /// If this string was "That dog" and you passed in a test string of "Th" and a 
        /// completionEnd of 'End' you would receive "at dog". If you passed in a
        /// completionEnd of 'Beginning' however, you would recieve "That dog" because
        /// a partial match could not be made of in front of the test string of "Th".
        /// 
        /// Similarly if the string was "That dog" and you passed in a test string of "og"
        /// with a completionEnd of 'Beginning' you would recieve "That d", and if you
        /// asked for a completionEnd of 'End' you would recieve "That dog".
        /// </remarks>
        /// <returns>
        ///		<para>The missing part of the test string needed to complete this string. Empty string if no</para>
        ///		<pata> completion is needed (full match with test), or this string if not a match at all.</pata>
        ///	</returns>
        public static string GetCompletionString(this string this_StringToComplete, string in_TestString, eCompletionEnd completionEnd)
        {
            if (in_TestString.Length > this_StringToComplete.Length)
            {
                if (completionEnd == eCompletionEnd.End)
                    in_TestString = in_TestString.Substring(in_TestString.Length - this_StringToComplete.Length);
                else
                    in_TestString = in_TestString.Substring(0, this_StringToComplete.Length);
            }

            // If it's an exact match, then we have no need for the test string
            if (in_TestString == this_StringToComplete)
                return "";

            // Otherwise check if it's a partial match, and return the portion not added
            if (in_TestString.Length < this_StringToComplete.Length)
            {
                if (completionEnd == eCompletionEnd.End)
                {
                    string sCompletion = this_StringToComplete.Substring(this_StringToComplete.Length - (this_StringToComplete.Length - in_TestString.Length));
                    if (in_TestString + sCompletion == this_StringToComplete)
                        return sCompletion;
                }
                else
                {
                    string sCompletion = this_StringToComplete.Substring(0, this_StringToComplete.Length - in_TestString.Length);
                    if (sCompletion + in_TestString == this_StringToComplete)
                        return sCompletion;
                }
            }
            string sShouldMatch_Partial;
            if (completionEnd == eCompletionEnd.End)
                sShouldMatch_Partial = this_StringToComplete[this_StringToComplete.Length - 1].ToString();
            else
                sShouldMatch_Partial = this_StringToComplete[0].ToString();
            while (sShouldMatch_Partial.Length < in_TestString.Length)
            {
                if (completionEnd == eCompletionEnd.End)
                {
                    string sCompletion = in_TestString.Substring(sShouldMatch_Partial.Length);
                    if (sCompletion + sShouldMatch_Partial == this_StringToComplete)
                    {
                        return sShouldMatch_Partial;
                    }
                    sShouldMatch_Partial = this_StringToComplete[this_StringToComplete.Length - (sShouldMatch_Partial.Length + 1)].ToString() + sShouldMatch_Partial;
                }
                else
                {
                    string sCompletion = in_TestString.Substring(0, in_TestString.Length - sShouldMatch_Partial.Length);
                    if (sShouldMatch_Partial + sCompletion == this_StringToComplete)
                    {
                        return sShouldMatch_Partial;
                    }
                    sShouldMatch_Partial += this_StringToComplete[sShouldMatch_Partial.Length].ToString();
                }
            }

            return this_StringToComplete;
        }

        /// <summary>
        /// This is a little horrible, and I should deprecate it or speed it up. It will basically take any string and add to it until it's
        /// the desired size. Thus "T:-" and I say I want it to be 5 chars filled with '-' would result in "T:---".
        /// My guess is that I would use this for console formatting or something.
        /// </summary>
        public static string StringWithLetterCount(this string source, int count, bool shouldCapIfOver = true, char fillWithChar = ' ')
        {
            int sizeDifference = source.Length - count;
            if (sizeDifference > 0)
            {
                if (shouldCapIfOver)
                    return source.Substring(0, count);
            }
            else if (sizeDifference < 0)
            {
                while (sizeDifference++ < 0)
                {
                    source += fillWithChar;
                }
            }

            return source;
        }
        public static bool CheckLetter(this String source, int letterInd, params char[] possibles)
        {
            if (source == null || source.Length <= letterInd || possibles == null)
                return false;

            return possibles.Contains(source[letterInd]);
        }

        public static string PrefixWithAorAn(this string source, bool startWithUpperCase, bool tryToClearOutExtraneousPrefix = false, string concatWith = " ")
        {
            if (source.IsNullOrEmpty())
                return source;

            string eval = source;
            if (tryToClearOutExtraneousPrefix && (eval[0] < 'a' || eval[0] > 'Z' || (eval[0] > 'z' && eval[0] < 'Z')))
            {
                eval = eval.TrimStart(kSymbols);
                if (eval.Length == 0)
                    return string.Format("{0}{1}{2}", startWithUpperCase ? "A" : "a", concatWith, eval);
            }
            switch (eval[0])
            {
                case 'A':
                case 'a':
                case 'E':
                case 'e':
                case 'I':
                case 'i':
                case 'O':
                case 'o':
                case 'U':
                case 'u':
                case 'Y':
                case 'y':
                    return string.Format("{0}{1}{2}", startWithUpperCase ? "A" : "a", concatWith, eval);
                default:
                    return string.Format("{0}{1}{2}", startWithUpperCase ? "An" : "an", concatWith, eval);
            }
        }



        internal static string StringShortenerWorkhorse(this string source, int maxLength, bool isPath, eStringShortening shortenWhere, string removedCharactersIndicator)
        {
            // Only shorten if it's worthwhile
            if (source.Length <= maxLength + removedCharactersIndicator.Length)
                return source;

            switch (shortenWhere)
            {
                case eStringShortening.TakeFromMiddle:
                    {
                        if (isPath)
                        {
                            string sFilePart = string.Format("{0}\\{1}", removedCharactersIndicator, System.IO.Path.GetFileName(source));
                            string sBaseDir = System.IO.Path.GetDirectoryName(source);
                            return sBaseDir.Substring(0, maxLength - sFilePart.Length) + sFilePart;
                        }
                        else
                        {
                            int halfIndicatorLength = removedCharactersIndicator.Length / 2;
                            int leftMiddle = (maxLength / 2) - halfIndicatorLength;
                            int rightMiddle = (maxLength / 2) + (removedCharactersIndicator.Length - halfIndicatorLength);
                            return string.Format("{0}{1}{2}", source.Substring(0, leftMiddle), removedCharactersIndicator, source.Substring(rightMiddle));
                        }
                    }
                case eStringShortening.TakeFromEnd:
                    {
                        return string.Format("{0}{1}", source.Substring(0, maxLength - removedCharactersIndicator.Length), removedCharactersIndicator);
                    }

                default: return source.Substring(0, maxLength);
            }
        }
    }
}
