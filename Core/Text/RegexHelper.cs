namespace AJut.Text
{
    using System;
    using System.Text.RegularExpressions;

    public static class RegexHelper
    {
        public const int EntireLength = -1;

        [Flags]
        public enum PathMatchingOptions
        {
            FullPath = 0x01,
            RelativePath = 0x10,
            Either = FullPath | RelativePath
        }

        private static Regex kFileMatchWithDriveRoot = new Regex(@"(\w:((\\|/)([\w\d ~'\^\$\.\+\(\)]*))+)");
        private static Regex kFileMatchWithoutDriveRoot = new Regex(@"\b?(((\w([\w\d ~\^\$\.\+\(\)]*)(\\|/))+([\w\d ~\^\$\.\+\(\)]*)))");

        // ==================================
        // Specialty Match Functions
        // ==================================

        public static Match Match(string input, string patternFormat, params object[] patternArgs)
        {
            return Match(input, 0, -1, RegexOptions.Multiline, patternFormat, patternArgs);
        }
        public static Match Match(string input, RegexOptions options, string patternFormat, params object[] patternArgs)
        {
            return Match(input, 0, -1, options, patternFormat, patternArgs);
        }
        public static Match Match(string input, int startIndex, string patternFormat, params object[] patternArgs)
        {
            return Match(input, startIndex, EntireLength, RegexOptions.Multiline, patternFormat, patternArgs);
        }
        public static Match Match(string input, int startIndex, RegexOptions options, string patternFormat, params object[] patternArgs)
        {
            return Match(input, startIndex, EntireLength, options, patternFormat, patternArgs);
        }
        public static Match Match(string input, int startIndex, int length, string patternFormat, params object[] patternArgs)
        {
            return Match(input, startIndex, length, RegexOptions.Multiline, patternFormat, patternArgs);
        }

        public static Match Match(string input, int startIndex, int length, RegexOptions options, string patternFormat, params object[] patternArgs)
        {
            try
            {
                // This could throw a format exception if there are un-escaped { or } especially combined with zero arguments as the user
                //  isn't expecting to have to worry about such things. That's the reason for not just passing it in.
                string pattern = patternArgs.IsNullOrEmpty() ? patternFormat : String.Format(patternFormat, patternArgs);

                Regex r = new Regex(pattern, RegexOptions.Multiline);
                return r.Match(input, startIndex, length == EntireLength ? input.Length - startIndex : length);
            }
            catch
            {
                return null;
            }
        }

        public static Capture MatchFilePath(string input, int startIndex, int length = EntireLength, PathMatchingOptions pathMatching = PathMatchingOptions.Either)
        {
            try
            {
                Capture result = null;
                if (pathMatching.HasFlag(PathMatchingOptions.FullPath))
                {
                    result = kFileMatchWithDriveRoot.Match(input, startIndex).GetMostCompleteCapture();
                }

                if (result == null && pathMatching.HasFlag(PathMatchingOptions.RelativePath))
                {
                    result = kFileMatchWithoutDriveRoot.Match(input, startIndex).GetMostCompleteCapture();
                }

                return result;
            }
            catch
            {
                return null;
            }
        }

        // ==================================
        // Extensions 
        // ==================================

        // - Was Successfull

        public static bool WasSuccessfull(this Match match, bool atleastOneGroup = true)
        {
            return match != null && match.Success && (!atleastOneGroup || !match.Groups.IsNullOrEmpty());
        }

        public static bool WasSuccessfull(this Group group, bool atleastOneCapture = true)
        {
            return group != null && group.Success && (!atleastOneCapture || group.Captures.Count > 0);
        }

        // - Most Complete 

        public static Group GetMostCompleteCaptureGroup(this Match match)
        {
            if (!match.WasSuccessfull())
            {
                return null;
            }

            return match.Groups[0];
        }

        public static Capture GetMostCompleteCapture(this Match match)
        {
            if(!match.WasSuccessfull())
            {
                return null;
            }

            var group = match.GetMostCompleteCaptureGroup();
            return group.WasSuccessfull() ? group.Captures[0] : null;
        }

        // - Most Specific

        public static Group GetMostSpecificCaptureGroup(this Match match)
        {
            if (!match.WasSuccessfull())
            {
                return null;
            }

            return match.Groups[match.Groups.Count - 1];
        }

        public static Capture GetMostSpecificCapture(this Match match)
        {
            if (!match.WasSuccessfull())
            {
                return null;
            }

            var group = match.GetMostSpecificCaptureGroup();
            return group.WasSuccessfull() ? group.Captures[group.Captures.Count - 1] : null;
        }

        public static string GetMostSpecificCaptureText(this Match match)
        {
            if (!match.WasSuccessfull())
            {
                return null;
            }

            var capture = match.GetMostSpecificCapture();
            return capture == null ? null : capture.Value;
        }

        public static Capture GetMostSpecificCapture(this Match match, string captureName)
        {
            if (!match.WasSuccessfull())
            {
                return null;
            }

            Group captureGroup = match.Groups[captureName];
            if (!captureGroup.WasSuccessfull())
            {
                return null;
            }

            return captureGroup.Captures[captureGroup.Captures.Count - 1];
        }
    }
}
