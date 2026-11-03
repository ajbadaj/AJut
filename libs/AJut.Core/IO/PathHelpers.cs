namespace AJut.IO
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using AJut.Storage;

    public static class PathHelpers
    {
        // Read-only rather than const, so a build compiled against these keeps reading them, and the value isn't baked into it
        /// <summary>
        /// The longest full path Windows takes (MAX_PATH, less its terminating null). <see cref="EvaluatePathValidity"/> holds a
        /// path to it on Windows only.
        /// </summary>
        public static readonly int kMaxFullPathLength = 259;

        /// <summary>
        /// The longest directory part of a path Windows takes (MAX_PATH less room for an 8.3 file name).
        /// <see cref="EvaluatePathValidity"/> holds a path to it on Windows only.
        /// </summary>
        public static readonly int kMaxPathDirectoryLength = 247;

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

        /// <summary>
        /// Whether <paramref name="path"/> can be used as a path here, and why not when it can't: it must not be empty, must
        /// fit Windows' length limits on Windows, must hold no character the OS refuses, must not be drive-relative (c:test,
        /// or c: alone), and must not climb above its root with '..' (c:\..\x, \\server\share\..\x). A relative path's
        /// leading '..' is fine.
        /// </summary>
        public static Result EvaluatePathValidity (string path)
        {
            if (path.IsNullOrEmpty())
            {
                return Result.Error("Empty path");
            }

            if (IsDriveRelative(path))
            {
                return Result.Error($"Path is drive-relative: '{path.Substring(0, 2)}' has no separator after it");
            }

            // MAX_PATH is Windows' own limit: macOS, Linux, Android and iOS take far longer paths
            if (PathPlatform.LimitsPathLength && path.Length > kMaxFullPathLength)
            {
                return Result.Error("Path is too long");
            }

            try
            {
                int lastPathSeparatorElement = path.LastIndexOfAny(PathPlatform.Separators);
                if (PathPlatform.LimitsPathLength
                    && (lastPathSeparatorElement != -1)
                    && (lastPathSeparatorElement > kMaxPathDirectoryLength))
                {
                    return Result.Error("Path directory is too long");
                }

                var invalidPathChars = Path.GetInvalidPathChars();
                var invalidFileNameChars = Path.GetInvalidFileNameChars();

                // On Windows '/' separates as well as '\', so the path splits on either: split on '\' alone, a '/' would be
                //  left inside a part, where it's an invalid file name char. Elsewhere only '/' separates, and '\' is an
                //  ordinary character.
                char separator = PathPlatform.NormalizedSeparator;
                string splittablePath = PathPlatform.TakesBackslashAsSeparator ? path.Replace('/', '\\') : path;
                var allPathParts = PathHelpers.GetAllPathParts(splittablePath, separator).ToArray();
                for (int index = 0; index < allPathParts.Length; ++index)
                {
                    // If we're on the filename part (if any)
                    if (index == allPathParts.Length - 1 && !splittablePath.EndsWith(separator))
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

                if (ClimbsAboveItsRoot(allPathParts, separator))
                {
                    return Result.Error("Path climbs above its root with '..'");
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
        /// Whether <paramref name="path"/> can be used as a path here, as <see cref="EvaluatePathValidity"/> decides
        /// </summary>
        public static bool IsValidAsPath (string path)
        {
            return EvaluatePathValidity(path);
        }

        /// <summary>
        /// Create a version of the passed in path that is resolved, and ready for comparison: its '.' parts dropped, each '..'
        /// resolved, and lowercased where the platform ignores case in a path (Windows and macOS; Android, iOS and Linux tell
        /// 'Photos' from 'photos', so the case is kept there). On Windows every separator becomes '\', since '/' separates
        /// there too. Elsewhere only '/' separates, and the result keeps it, with any '\' left as part of its name. It is for
        /// comparing paths, not for opening one.
        /// </summary>
        /// <param name="path">The un-normalized path</param>
        /// <returns>The normalized version of the path</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// A '..' climbs above the path's root (a drive, a drive's root, or a network path's server and share: c:\..\x,
        /// \\server\share\..\x), or past the start of a path with no root; or the path is drive-relative (c:test, or c: alone)
        /// </exception>
        public static string NormalizePath (string path)
        {
            return NormalizePath(path, PathPlatform.IgnoresCase);
        }

        /// <summary>
        /// <see cref="NormalizePath(string)"/>, lowercasing only when <paramref name="ignoreCase"/> says to rather than by the
        /// platform's rule, for a caller that has to tell paths differing only by case from paths that match exactly
        /// </summary>
        internal static string NormalizePath (string path, bool ignoreCase)
        {
            // Nonsense input is refused rather than made into some other path. A drive-relative path names a place under
            //  its drive's current folder, which the text alone can't say (c:test is not c:\test).
            if (IsDriveRelative(path))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(path),
                    path,
                    $"The path is drive-relative: '{path.Substring(0, 2)}' has no separator after it"
                );
            }

            string givenPath = path;

            // On Windows every '/' becomes '\', and the path splits on that. Elsewhere it splits on '/', the only separator
            //  there, and a '\' stays part of its name: on Linux a\b and a/b are two different paths. Lowercasing is
            //  invariant, so the current culture can't change what matches (Turkish lowercases I to a dotless i).
            char separator = PathPlatform.NormalizedSeparator;
            if (ignoreCase)
            {
                path = path.ToLowerInvariant();
            }

            if (PathPlatform.TakesBackslashAsSeparator)
            {
                path = path.Replace('/', '\\');
            }

            List<string> pathParts = GetAllPathParts(path, separator).ToList();

            // A root has nothing above it, so a '..' in it or straight after it climbs above the root, the same as a '..'
            //  that climbs past the start of a path with no root. Neither is clamped to where it stops.
            int rootPartCount = CountRootParts(pathParts, separator);
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
                    if (index <= rootPartCount)
                    {
                        string problem = (rootPartCount == 0)
                            ? "The path climbs past its start with '..', and has no root to resolve it against"
                            : "The path climbs above its root with '..'";
                        throw new ArgumentOutOfRangeException(nameof(path), givenPath, problem);
                    }

                    // Remove the pair, then look next at what moved into its first slot, which can be another '..'
                    pathParts.RemoveAt(index);
                    pathParts.RemoveAt(index - 1);
                    index -= 2;
                }
            }

            // Joining drops a lone root's separator (the empty part of a drive's root gives nothing, and the network root's
            //  part gives one separator of the two), so it goes back on
            string normalized = String.Join(separator, pathParts);
            if ((pathParts.Count == 1)
                && ((pathParts[0].Length == 0) || ((pathParts[0].Length == 1) && (pathParts[0][0] == separator))))
            {
                normalized += separator;
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
        /// Generate a relative path between the source directory and the target path. Both split and match by the platform's
        /// rules: '\' and '/' separate on Windows and only '/' elsewhere, and folder names ignore case on Windows and macOS
        /// and tell it apart on Android, iOS and Linux.
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

            List<string> source = sourceDirectory.Split(PathPlatform.Separators, StringSplitOptions.RemoveEmptyEntries).ToList();
            List<string> target = targetPath.Split(PathPlatform.Separators, StringSplitOptions.RemoveEmptyEntries).ToList();

            int finalMatchIndex = -1;
            int shortestSequence = Math.Min(source.Count, target.Count);
            for (int index = 0; index < shortestSequence; ++index)
            {
                if (!source[index].Equals(target[index], PathPlatform.Comparison))
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
        /// Provides all the extensions in the filter, in the order its patterns give them, each once and without its dot:
        /// <c>jpg</c> for <c>*.jpg</c>, the name itself for a filter written as a file name (<c>ajut.core.dll</c> for
        /// <c>*ajut.core.dll</c>), and <c>*</c> for <c>*.*</c>. Only the patterns are read, never the descriptions beside
        /// them, and a filter can be its patterns alone (<c>*.txt;*.md</c>). Null or empty gives none.
        /// </summary>
        /// <param name="fileFilterString">Path filter of expected format: Thing (*.thing)|*.thing, with additional items separated by vertical bar (pipe), and a group's patterns by semicolon</param>
        /// <returns>Each extension</returns>
        public static string[] ParseExtensionsFrom (string fileFilterString)
        {
            List<string> extensions = new List<string>();
            if (fileFilterString.IsNullOrEmpty())
            {
                return extensions.ToArray();
            }

            // A filter alternates description and patterns (Text (*.txt)|*.txt|Images|*.png;*.jpg), so its patterns are every
            //  second part, starting with the second. A description is display text, and one written as "Images (*.png;*.jpg)"
            //  must not be read as patterns too. A filter with no bar at all is nothing but patterns.
            string[] parts = fileFilterString.Split('|');
            int firstPatternsIndex = (parts.Length == 1) ? 0 : 1;
            for (int index = firstPatternsIndex; index < parts.Length; index += 2)
            {
                foreach (string pattern in parts[index].Split(';'))
                {
                    // The wildcard and dot in front go (*.jpg gives jpg, *ajut.core.dll gives ajut.core.dll), and a pattern
                    //  that is nothing but those (*.*, or *) matches any file
                    string trimmedPattern = pattern.Trim();
                    if (trimmedPattern.Length == 0)
                    {
                        continue;
                    }

                    string extension = trimmedPattern.TrimStart('*', '.');
                    extensions.AddIfUnique(extension.Length == 0 ? "*" : extension);
                }
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
        /// ajut.core.dll; <c>*</c> matches any file. Case is ignored, the same in every culture. The file name is what follows
        /// the path's last separator: '\' or '/' on Windows, and only '/' elsewhere, where '\' is part of a name.
        /// </summary>
        public static IEnumerable<string> FindMatchingExtensions (string filePath, params string[] extensions)
        {
            string fileName = filePath.Substring(filePath.LastIndexOfAny(PathPlatform.Separators) + 1);
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

        /// <summary>
        /// Whether every one of <paramref name="paths"/> (nulls aside) names the same place, compared through
        /// <see cref="NormalizePath(string)"/>, so case counts only where the platform tells it apart. It compares the text,
        /// not the disk: a volume that differs from its platform's rule (an exFAT card on Android, a case-sensitive APFS
        /// volume on a Mac) isn't asked.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A path <see cref="NormalizePath(string)"/> can't normalize</exception>
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

        /// <summary>
        /// Whether <paramref name="path"/> is drive-relative: a drive with no separator after it (c:test, or c: alone), which
        /// names a place under that drive's current folder rather than under its root. Off Windows, where '\' is part of a
        /// name, c:\test is one too.
        /// </summary>
        private static bool IsDriveRelative (string path)
        {
            return (path.Length >= 2)
                && (path[1] == ':')
                && ((path.Length == 2) || !PathPlatform.IsSeparator(path[2]));
        }

        /// <summary>
        /// How many of <paramref name="pathParts"/> (from <see cref="GetAllPathParts(string, char)"/>) are the path's root: none
        /// for a path with no root, one for a drive (c:) or a drive's root (the empty part of \a\b), and for a network path its
        /// root part (the separator), its server and its share, as Windows takes \\server\share
        /// </summary>
        private static int CountRootParts (IReadOnlyList<string> pathParts, char pathSeparator)
        {
            if (pathParts.Count == 0)
            {
                return 0;
            }

            string first = pathParts[0];
            if ((first.Length == 0) || ((first.Length == 2) && (first[1] == ':')))
            {
                return 1;
            }

            const int kNetworkRootPartCount = 3;
            if ((first.Length == 1) && (first[0] == pathSeparator))
            {
                return Math.Min(kNetworkRootPartCount, pathParts.Count);
            }

            return 0;
        }

        /// <summary>
        /// Whether a '..' in <paramref name="pathParts"/> climbs above the path's root (see <see cref="CountRootParts"/>). A path
        /// with no root never does: its leading '..' goes above wherever it's taken from, which is an ordinary relative path.
        /// </summary>
        private static bool ClimbsAboveItsRoot (IReadOnlyList<string> pathParts, char pathSeparator)
        {
            int rootPartCount = CountRootParts(pathParts, pathSeparator);
            if (rootPartCount == 0)
            {
                return false;
            }

            int depthBelowTheRoot = 0;
            for (int index = 0; index < pathParts.Count; ++index)
            {
                string part = pathParts[index];
                if (part == "..")
                {
                    // A '..' that is a server or share name, or that has nothing below the root to climb out of
                    if ((index < rootPartCount) || (depthBelowTheRoot == 0))
                    {
                        return true;
                    }

                    --depthBelowTheRoot;
                }
                else if ((index >= rootPartCount) && (part != "."))
                {
                    ++depthBelowTheRoot;
                }
            }

            return false;
        }
    }
}
