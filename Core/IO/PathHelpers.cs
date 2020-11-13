namespace AJut.IO
{
    using System.Linq;
    using System.IO;
    using System.Collections.Generic;
    using System;
    using System.Text;

    public static class PathHelpers
    {
        public static int kMaxFullPathLength = 259;
        public static int kMaxPathDirectoryLength = 247;

        /// <summary>
        /// Makes sure the passed in string is not null, and doesn't contain any invalid path chars
        /// </summary>
        public static bool IsValidAsPath(string path)
        {
            if(path == null)
            {
                return false;
            }

            if(path.Length > kMaxFullPathLength)
            {
                return false;
            }

            try
            {
                int pathSep = path.LastIndexOf('\\');
                int otherPathSep = path.LastIndexOf('/');
                if(pathSep == -1)
                {
                    pathSep = otherPathSep;
                }
                else if(otherPathSep != -1 && otherPathSep > pathSep)
                {
                    pathSep = otherPathSep;
                }

                if (pathSep == -1 || pathSep <= kMaxPathDirectoryLength)
                {
                    return Path.GetInvalidPathChars().All(invalidPathChar => !path.Contains(invalidPathChar));
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Create a version of the passed in path that is resolved, and ready for comparison.
        /// </summary>
        /// <param name="path">The un-normalized path</param>
        /// <returns>Either a normalized version of the path, or <c>null</c> if the path couldn't be normalized.</returns>
        public static string Normalize(string path)
        {
            if(!IsValidAsPath(path))
            {
                return null;
            }

            string originalPath = path;

            List<string> pathStack = new List<string>();

            path = path.Replace('/', '\\').Trim(' ').ToLower();
            bool isPathRooted = path[1] == ':';
            if (isPathRooted)
            {
                if(path[0] < 'a' || path[0] > 'z' || path[2] != '\\')
                {
                    throw new Exception(String.Format("You provided an improperly formatted path, with a weird path rooting: '{0}'", path));
                }

                pathStack.Add(path.Substring(0, 2)); // ie c:
                path = path.SubstringFromRelativeEnd(3, 0); // Skipping the first path sep
            }

            int nextStartIndex = 0;
            int nextPathSepIndex = path.IndexOf('\\');
            if(nextPathSepIndex == -1)
            {
                return path;
            }

            while (nextPathSepIndex != -1)
            {
                if (nextStartIndex != nextPathSepIndex)
                {
                    string pathPiece = path.SubstringByInd(nextStartIndex, nextPathSepIndex - 1);
                    switch (pathPiece)
                    {
                        case "..":
                            if(pathStack.Count == 0)
                            {
                                throw new Exception(String.Format("Relative directory specification made that goes past specified root: '{0}'", path));
                            }
                            pathStack.RemoveAt(pathStack.Count - 1);
                            break;

                        default:
                            pathStack.Add(pathPiece);
                            break;
                    }
                }

                nextStartIndex = nextPathSepIndex + 1;
                nextPathSepIndex = path.IndexOf('\\', nextStartIndex);
            }

            string output = String.Join("\\", pathStack.ToArray());
            if (nextStartIndex != -1 && nextStartIndex < path.Length - 1)
            {
                output += "\\" + path.Substring(nextStartIndex);
            }

            return output;
        }

        /// <summary>
        /// Utility that removes all <see cref="Path"/>.GetInvalidFileNameChars() from the passed in file name.
        /// </summary>
        /// <param name="fileName">The file name to sanitize</param>
        /// <returns>The a version of the passed in file name that has no <see cref="Path"/>.GetInvalidFileNameChars() in it</returns>
        public static string SanitizeFileName(string fileName)
        {
            if(fileName == null)
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
        public static string SanitizePath(string path)
        {
            if (path == null)
            {
                return null;
            }

            StringBuilder outputPath = new StringBuilder(path.Length);
            char[] invalidPathChars = Path.GetInvalidPathChars();
            foreach (char pathChar in path)
            {
                if(!invalidPathChars.Contains(pathChar))
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

        public static string ShortenPath(this string This, int maxLength, eStringShortening shortenWhere)
        {
            return StringXT.StringShortenerWorkhorse(This, maxLength, true, shortenWhere, "...");
        }
        public static string ShortenPath(this string This, int maxLength, eStringShortening shortenWhere, string removedCharactersIndicator)
        {
            return StringXT.StringShortenerWorkhorse(This, maxLength, true, shortenWhere, removedCharactersIndicator);
        }
    }
}
