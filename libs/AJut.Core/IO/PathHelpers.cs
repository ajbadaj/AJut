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

        /// <summary>
        /// Splits a path into its parts on <see cref="Path.DirectorySeparatorChar"/>, as <see cref="GetAllPathParts(string, char)"/> does.
        /// </summary>
        public static IEnumerable<string> GetAllPathParts (string path)
        {
            return GetAllPathParts(path, Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Splits a path into its parts, without the separators. A root is a part of its own: a drive's (c:), the network
        /// root's (the separator, for \\server\share), or an empty part for a path rooted at its drive's root (\a\b, or
        /// /a/b off Windows), so joining the parts with the separator gives the path back. A relative path keeps a leading
        /// . or .. as a part.
        /// </summary>
        public static IEnumerable<string> GetAllPathParts (string path, char pathSeparator)
        {
            // 1. Is path rooted (ie c:\)
            // 2. Is network path (ie \\test)
            // 3. Is path rooted at its drive's root (ie \test, or /test off Windows)
            // 4. Is relative path (ie .\here\there or here\there or ..\here\there), which splits like any other and keeps a
            //    leading . or .. as a part of its own
            int start;
            int nextSeparator;

            // A rooted path
            if (path.Length >= 2 && path[1] == ':')
            {
                // should maybe establish that path[0] is valid
                yield return path.Substring(0,2);

                start = 2;
                nextSeparator = path.IndexOf(pathSeparator, start);
            }
            // A network path
            else if (path.Length >= 2 && path[0] == pathSeparator && path[1] == pathSeparator)
            {
                yield return pathSeparator.ToString();
                start = 2;
                nextSeparator = path.IndexOf(pathSeparator, start);
            }
            // A path rooted at its drive's root: the empty part is its root, which joining the parts gives back
            else if (path.Length >= 1 && path[0] == pathSeparator)
            {
                yield return String.Empty;
                start = 1;
                nextSeparator = path.IndexOf(pathSeparator, start);
            }
            // A relative path
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

                // On Windows '/' separates as well as '\', so the path splits on either: split on '\' alone, a '/' would be
                //  left inside a part, where it's an invalid file name char. Elsewhere both separator chars are '/', and '\'
                //  is an ordinary character.
                string splittablePath = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                var allPathParts = PathHelpers.GetAllPathParts(splittablePath).ToArray();
                for (int index = 0; index < allPathParts.Length; ++index)
                {
                    // If we're on the filename part (if any)
                    if (index == allPathParts.Length - 1 && !splittablePath.EndsWith(Path.DirectorySeparatorChar))
                    {
                        if (_ContainsChars(allPathParts[index], invalidFileNameChars, out string foundChars))
                        {
                            return Result.Error($"File name contains invalid path chars: {foundChars}");
                        }
                    }
                    else if (_ContainsChars(allPathParts[index], invalidPathChars, out string foundChars))
                    {
                        return Result.Error($"Path contains invalid path chars: {foundChars}");
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
        /// Create a version of the passed in path that is resolved, and ready for comparison: lowercased, with '\' for every
        /// separator, its '.' parts dropped and each '..' resolved. It is for comparing paths, not for opening one.
        /// </summary>
        /// <param name="path">The un-normalized path</param>
        /// <returns>The normalized version of the path</returns>
        /// <exception cref="ArgumentOutOfRangeException">A '..' climbs past the start of a path with no root</exception>
        public static string NormalizePath (string path)
        {
            // Split on '\', the separator the path was just converted to. The OS's separator is '/' off Windows, and
            //  splitting on that would leave the whole path as one part. Lowercasing is invariant, so the current culture
            //  can't change what matches (Turkish lowercases I to a dotless i).
            path = path.ToLowerInvariant().Replace('/', '\\');
            List<string> pathParts = GetAllPathParts(path, '\\').ToList();

            // A root has nothing above it, whether a drive (c:), the network root (\) or the empty part of a path rooted
            //  at its drive's root. So a '..' straight after one goes and the root stays, as Path.GetFullPath does. Only
            //  an unrooted path can climb past its start, which throws.
            bool startsAtARoot = pathParts.Count > 0
                && ((pathParts[0].Length == 2 && pathParts[0][1] == ':') || pathParts[0] == "\\" || pathParts[0].Length == 0);

            for (int index = 0; index < pathParts.Count; ++index)
            {
                // A '.' names where the path already is
                if (pathParts[index] == ".")
                {
                    pathParts.RemoveAt(index);
                    index -= 1;
                }
                else if (pathParts[index] == "..")
                {
                    if (index == 0)
                    {
                        throw new ArgumentOutOfRangeException($"Relative directory specification made that goes past specified root: '{path}'");
                    }

                    if (index == 1 && startsAtARoot)
                    {
                        pathParts.RemoveAt(index);
                        index -= 1;
                    }
                    else
                    {
                        // Remove the pair, then look next at what moved into its first slot, which can be another '..'
                        pathParts.RemoveAt(index);
                        pathParts.RemoveAt(index - 1);
                        index -= 2;
                    }
                }
            }

            // Joining drops a lone root's separator (the empty part of \ gives nothing, the \ part of \\ gives one \), so
            //  it goes back on
            string normalized = String.Join('\\', pathParts);
            if (pathParts.Count == 1 && (pathParts[0].Length == 0 || pathParts[0] == "\\"))
            {
                normalized += '\\';
            }

            return normalized;
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
        /// <param name="path">The path to sanitize</param>
        /// <returns>The a version of the passed in file name that has no <see cref="Path"/>.GetInvalidPathChars() in it</returns>
        public static string SanitizePath (string path)
        {
            if (path == null)
            {
                return null;
            }

            StringBuilder outputPath = new StringBuilder(path.Length);
            char[] invalidPathChars = Path.GetInvalidPathChars();
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
        /// <param name="prefix">An optional prefix to prepend</param>
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
                if (!source[index].Equals(target[index], StringComparison.OrdinalIgnoreCase))
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
        private static readonly Regex kExtensionParser = new Regex(@"[|;](?:[\*\.]+|[\*])([\w\d\-\.\*]+)(?:$|\||;)");

        /// <summary>
        /// Provides all the extensions in the filter, each without its dot: <c>jpg</c> for <c>*.jpg</c>, the name itself for
        /// a filter written as a file name (<c>ajut.core.dll</c> for <c>*ajut.core.dll</c>), and <c>*</c> for <c>*.*</c>
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

        /// <summary>
        /// Finds which of <paramref name="extensions"/> the file has. An extension, with or without its dot, matches at a dot
        /// or as the whole file name, so <c>jpg</c> matches photo.jpg but not notajpg, and <c>ajut.core.dll</c> matches
        /// ajut.core.dll; <c>*</c> matches any file. Case is ignored, the same in every culture.
        /// </summary>
        public static IEnumerable<string> FindMatchingExtensions (string filePath, params string[] extensions)
        {
            string fileName = filePath.Substring(filePath.LastIndexOfAny(new[] { '\\', '/' }) + 1);
            return extensions.Where(_IsMatch);

            bool _IsMatch (string _extension)
            {
                if (_extension == "*")
                {
                    return true;
                }

                string _bareExtension = _extension.TrimStart('.');
                return fileName.Equals(_bareExtension, StringComparison.OrdinalIgnoreCase)
                    || fileName.EndsWith("." + _bareExtension, StringComparison.OrdinalIgnoreCase);
            }
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

            HashSet<string> hash = new HashSet<string>();
            foreach (string path in paths.Where(p => p != null).Select(p => NormalizePath(p)))
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
