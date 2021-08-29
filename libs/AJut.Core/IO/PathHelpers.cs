namespace AJut.IO
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using AJut.Storage;

    public static class PathHelpers
    {
        public static int kMaxFullPathLength = 259;
        public static int kMaxPathDirectoryLength = 247;

        public const string kAnyFileFilter = "Any File (*.*)|*.*";

        public static IEnumerable<string> GetAllPathParts (string path)
        {
            return GetAllPathParts(path, Path.DirectorySeparatorChar);
        }

        public static IEnumerable<string> GetAllPathParts (string path, char pathSeparator)
        {
            // 1. Is path rooted (ie c:\)
            // 2. Is network path (ie \\test)
            // 3. Is relative path (ie .\here\there or here\there or ..\here\there)
            if (path.Length < 3)
            {
                yield return path;
                yield break;
            }

            int start;
            int nextSeparator;

            // A rooted path
            if (path[1] == ':')
            {
                // should maybe establish that path[0] is valid
                yield return path.Substring(0,2);

                start = 2;
                nextSeparator = path.IndexOf(pathSeparator, start);
            }
            // A network path
            else if (path[0] == pathSeparator && path[1] == pathSeparator)
            {
                yield return pathSeparator.ToString();
                start = 2;
                nextSeparator = path.IndexOf(pathSeparator, start);
            }
            // A relative path
            else if (path[0] == '.')
            {
                // ..\path
                if (path[1] == '.' && path[2] == pathSeparator)
                {
                    start = path.IndexOf(pathSeparator, 3);
                    if (start == -1)
                    {
                        yield break;
                    }

                    start += 1;
                    nextSeparator = path.IndexOf(pathSeparator, start);
                }
                // .\path
                else if (path[1] == pathSeparator)
                {
                    start = path.IndexOf(pathSeparator, 2);
                    nextSeparator = path.IndexOf(pathSeparator, start);
                }

                // Invalid
                else
                {
                    yield break;
                }
            }
            else
            {
                start = 0;
                nextSeparator = path.IndexOf(pathSeparator, start);
            }

            while (nextSeparator != -1)
            {
                string subdir = path.SubstringByInd(start, nextSeparator - 1);
                if (subdir.IsNotNullOrEmpty())
                {
                    yield return subdir;
                }
                start = nextSeparator + 1;
                nextSeparator = path.IndexOf(pathSeparator, start);
            }
            if (start < path.Length)
            {
                yield return path.Substring(start);
            }
        }

        public static Result EvaluatePathValidity (string path)
        {
            if (path.IsNullOrEmpty())
            {
                return Result.Error("Empty path");
            }

            if (path.Length > kMaxFullPathLength)
            {
                return Result.Error("Path is too long");
            }

            try
            {
                int lastPathSeparatorElement = path.LastIndexOf('\\');
                int otherPathSep = path.LastIndexOf('/');
                if (lastPathSeparatorElement == -1)
                {
                    lastPathSeparatorElement = otherPathSep;
                }
                else if (otherPathSep != -1 && otherPathSep > lastPathSeparatorElement)
                {
                    lastPathSeparatorElement = otherPathSep;
                }

                if (lastPathSeparatorElement != -1 && lastPathSeparatorElement > kMaxPathDirectoryLength)
                {
                    return Result.Error("Path directory is too long");
                }

                var invalidPathChars = Path.GetInvalidPathChars();
                var invalidFileNameChars = Path.GetInvalidFileNameChars();
                var allPathParts = PathHelpers.GetAllPathParts(path).ToArray();
                for (int index = 0; index < allPathParts.Length; ++index)
                {
                    // If we're on the filename part (if any)
                    if (index == allPathParts.Length - 1 && !path.EndsWith(Path.DirectorySeparatorChar))
                    {
                        if (_ContainsChars(allPathParts[index], invalidFileNameChars, out string foundChars))
                        {
                            return Result.Error($"File name contains invalid path chars → {foundChars}");
                        }
                    }
                    else if (_ContainsChars(allPathParts[index], invalidPathChars, out string foundChars))
                    {
                        return Result.Error($"Path contains invalid path chars → {foundChars}");
                    }
                }

                return Result.Success();
            }
            catch (Exception e)
            {
                return Result.Error($"Unknown error: {e}");
            }

            bool _ContainsChars (string _pathPart, char[] _chars, out string foundChars)
            {
                char[] _allFound = _chars.Where(c => _pathPart.Contains(c)).ToArray();
                if (_allFound.Length > 0)
                {
                    foundChars = $"'{String.Join("', '", _allFound)}'";
                    return true;
                }

                foundChars = null;
                return false;
            }
        }


        /// <summary>
        /// Makes sure the passed in string is not null, and doesn't contain any invalid path chars
        /// </summary>
        public static bool IsValidAsPath (string path)
        {
            return EvaluatePathValidity(path);
        }

        /// <summary>
        /// Create a version of the passed in path that is resolved, and ready for comparison.
        /// </summary>
        /// <param name="path">The un-normalized path</param>
        /// <returns>Either a normalized version of the path, or <c>null</c> if the path couldn't be normalized.</returns>
        public static string NormalizePath (string path)
        {
            path = path.ToLower().Replace('/', '\\');
            List<string> pathParts = GetAllPathParts(path).ToList();
            for (int index = 0; index < pathParts.Count; ++index)
            {
                if (pathParts[index] == "..")
                {
                    if (index == 0)
                    {
                        throw new ArgumentOutOfRangeException($"Relative directory specification made that goes past specified root: '{path}'");
                    }

                    pathParts.RemoveAt(index);
                    pathParts.RemoveAt(index - 1);
                    index -= 1;
                }
            }

            return String.Join('\\', pathParts);
        }

        /// <summary>
        /// Utility that removes all <see cref="Path"/>.GetInvalidFileNameChars() from the passed in file name.
        /// </summary>
        /// <param name="fileName">The file name to sanitize</param>
        /// <returns>The a version of the passed in file name that has no <see cref="Path"/>.GetInvalidFileNameChars() in it</returns>
        public static string SanitizeFileName (string fileName)
        {
            if (fileName == null)
            {
                return null;
            }

            StringBuilder outputFileName = new StringBuilder(fileName.Length);
            char[] invalidPathChars = Path.GetInvalidFileNameChars();
            foreach (char fileNameChar in fileName)
            {
                if (!invalidPathChars.Contains(fileNameChar))
                {
                    outputFileName.Append(fileNameChar);
                }
            }

            return outputFileName.ToString();
        }

        /// <summary>
        /// Utility that removes all <see cref="Path"/>.GetInvalidPathChars() from the passed in file name.
        /// </summary>
        /// <param name="fileName">The file name to sanitize</param>
        /// <returns>The a version of the passed in file name that has no <see cref="Path"/>.GetInvalidPathChars() in it</returns>
        public static string SanitizePath (string path)
        {
            if (path == null)
            {
                return null;
            }

            StringBuilder outputPath = new StringBuilder(path.Length);
            char[] invalidPathChars = Path.GetInvalidFileNameChars();
            foreach (char pathChar in path)
            {
                if (!invalidPathChars.Contains(pathChar))
                {
                    outputPath.Append(pathChar);
                }
            }

            return outputPath.ToString();
        }

        /// <summary>
        /// Generate a relative path between the source directory and the target path
        /// </summary>
        /// <param name="sourceDirectory">The source directory</param>
        /// <param name="targetPath">The target path</param>
        /// <param name="separatorChar">The character to use when building the final path</param>
        /// <returns>A relative path from the target directory to the source directory</returns>
        public static string GenerateRelativePath (string sourceDirectory, string targetPath, char separatorChar = '\\', string prefix = null)
        {
            if (!PathHelpers.IsValidAsPath(sourceDirectory) || !PathHelpers.IsValidAsPath(targetPath))
            {
                return null;
            }

            List<string> source = sourceDirectory.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            List<string> target = targetPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            int finalMatchIndex = -1;
            int shortestSequence = Math.Min(source.Count, target.Count);
            for (int index = 0; index < shortestSequence; ++index)
            {
                if (!source[index].Equals(target[index], StringComparison.CurrentCultureIgnoreCase))
                {
                    finalMatchIndex = index;
                    break;
                }
            }

            // They were total matches so far
            if (finalMatchIndex == -1)
            {
                // If they are the same
                if (source.Count == target.Count)
                {
                    return prefix ?? String.Empty;
                }

                finalMatchIndex = shortestSequence;
            }

            // They were rooted in completely different places, that means the target is rooted relative to something else
            //  that means we CAN'T determine a relative offset so return null
            if (finalMatchIndex == 0)
            {
                return null;
            }

            // Build the relative dirs
            StringBuilder output = new StringBuilder();
            int numUpADirAdds = source.Count - finalMatchIndex;
            finalMatchIndex = source.Count;
            while (numUpADirAdds-- > 0)
            {
                output.Append($"..{separatorChar}");
                prefix = null;
                finalMatchIndex -= 1;
            }

            if (prefix != null)
            {
                output.Append(prefix);
            }

            output.Append(String.Join(separatorChar.ToString(), target.Skip(finalMatchIndex)));
            return output.ToString();
        }

        public static string ShortenPath (this string This, int maxLength, eStringShortening shortenWhere)
        {
            return StringXT.StringShortenerWorkhorse(This, maxLength, true, shortenWhere, "...");
        }
        public static string ShortenPath (this string This, int maxLength, eStringShortening shortenWhere, string removedCharactersIndicator)
        {
            return StringXT.StringShortenerWorkhorse(This, maxLength, true, shortenWhere, removedCharactersIndicator);
        }

        /// <summary>
        /// Regex Breakdown:
        ///     1. Look for: *.
        ///     2. Look for extension text, words, numbers,
        /// </summary>
        private static readonly Regex kExtensionParser = new Regex(@"[|;]\*.([\*\w\d_-]*)");

        /// <summary>
        /// Provides all the extensions in format: <code>.extension</code>
        /// </summary>
        /// <param name="fileFilterString">Path filter of expected format: Thing (*.thing)|*.thing, with additional items separated by vertical bar (pipe)</param>
        /// <returns>Enumerable of each extension</returns>
        public static string[] ParseExtensionsFrom (string fileFilterString)
        {
            List<string> extensions = new List<string>();
            for (Match match = kExtensionParser.Match(fileFilterString); match?.Success == true; match = match.NextMatch())
            {
                // Result (if successful) of the above capture will be 4 groups:
                //  Group[0] = Entire capture, |*.extension|
                //  Group[1] = First bar, |
                //  Group[2] = Inside the bars, *.extension
                //  Group[3] = Last bar, |
                //
                // The only time it will deviate will be if format is a bit unexpected, and we are start of line or end of line, eliminating
                //  Group[1] or Group[3] respectively.
                extensions.AddIfUnique(match.Groups[match.Groups.Count - 1].Value.TrimEnd(';', ' '));
            }

            return extensions.ToArray();
        }

        public static IEnumerable<string> FindMatchingExtensionsFromFilter (string filePath, string fileFilterString)
        {
            return FindMatchingExtensions(filePath, ParseExtensionsFrom(fileFilterString));
        }

        public static IEnumerable<string> FindMatchingExtensions (string filePath, params string[] extensions)
        {
            return extensions.Where(e => e == "*" || filePath.EndsWith(e, StringComparison.CurrentCultureIgnoreCase));
        }

        public static string CreateFileFilterFor (params FileType[] fileTypes)
        {
            return String.Join("|", fileTypes.Select(_ => _.FileFilter));
        }

        public static string CreateGroupFilter (string name, params FileType[] fileTypes)
        {
            return $"{name}|*.{String.Join(";*.", fileTypes.SelectMany(ft => ft.Extensions))}";
        }

        public static bool ArePathsMatching (params string[] paths)
        {
            if (paths == null)
            {
                return false;
            }

            if (paths.Length < 2)
            {
                return true;
            }

            string[] temp = paths.Select(p => NormalizePath(p)).ToArray();

            HashSet<string> hash = new HashSet<string>();
            foreach (string path in paths.Select(p => NormalizePath(p)))
            {
                if (hash.Add(path) && hash.Count > 1)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
