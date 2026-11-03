namespace AJut.UX.Controls
{
    using System;
    using System.IO;
    using System.Linq;
    using AJut;
    using AJut.IO;
    using AJut.Storage;

    // ===========[ PathSelectionRules ]=========================================
    // What every stack's PathSelectionControl decides about a path, with no toolkit in it: whether the path is valid and
    // why not, whether it is there, where browsing starts, how a path is kept relative to FixedRootPath, and the folder
    // above it. Each control only shows the result and runs the picker, so the same path gets the same answer, and the
    // same reason, on every stack.
    //
    // A path is checked in this order, and the first thing wrong with it is the reason given:
    //  1. Empty: fine unless an empty path is to be treated as invalid
    //  2. PathHelpers.EvaluatePathValidity: characters, length and the rest of what makes text a path at all
    //  3. A rooted path must be under FixedRootPath, when there is one (a relative path is taken to be under it)
    //  4. A file path must have one of the filter's extensions
    //  5. A path that is there must be the kind asked for: a file for a file, a folder for a folder
    //  6. A path that is not there is only invalid when a missing path is to be treated as invalid
    /// <summary>
    /// The rules every stack's PathSelectionControl follows to judge a path, toolkit free so each one gives the same
    /// answer and the same reason
    /// </summary>
    public static class PathSelectionRules
    {
        // Every character that separates a path here: '\' and '/' on Windows, '/' elsewhere, where both of these are '/'
        private static readonly char[] kSeparators = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Judges <paramref name="path"/> as a PathSelectionControl's selected path: whether it is valid (and if not, why
        /// not), and whether it is there. A relative path is looked for under <paramref name="fixedRootPath"/>.
        /// </summary>
        /// <param name="path">The path, as the user typed or picked it</param>
        /// <param name="pathType">Whether the path is meant to name a file or a folder</param>
        /// <param name="fileFilter">The file filter (Description|*.ext;*.ext2), which a file path's extension must match</param>
        /// <param name="fixedRootPath">A folder a rooted path must be under, or null for none</param>
        /// <param name="treatEmptyPathAsInvalid">Whether an empty path is invalid rather than just unset</param>
        /// <param name="treatNonExistentPathAsInvalid">Whether a path that isn't there is invalid</param>
        public static Evaluation Evaluate (string path, ePathType pathType, string fileFilter, string fixedRootPath, bool treatEmptyPathAsInvalid, bool treatNonExistentPathAsInvalid)
        {
            // Empty is allowed unless it's to be treated as invalid, and nothing else can be said about it
            if (!treatEmptyPathAsInvalid && path.IsNullOrEmpty())
            {
                return new Evaluation(IsPathValid: true, DoesPathExist: false, InvalidPathReason: null);
            }

            Result result = PathHelpers.EvaluatePathValidity(path);
            if (!result)
            {
                return Invalid(result.GetErrorReport());
            }

            if (fixedRootPath.IsNotNullOrEmpty()
                && Path.IsPathRooted(path)
                && !TryGetPartBelowFixedRoot(path, fixedRootPath, out _))
            {
                return Invalid($"Path does not indicate a location under the fixed root path (fixed root path is '{fixedRootPath}')");
            }

            if (pathType == ePathType.File && !HasAllowedExtension(path, fileFilter))
            {
                // The extensions are listed after the path, cut down to about as long as the path (or a line) allows
                const int kMinimumDisplayLength = 75;
                const double kDisplayLengthScale = 1.3;
                int extensionsDisplayLength = (int)(Math.Max(path.Length, kMinimumDisplayLength) * kDisplayLengthScale);
                string extensions = String.Join(", ", PathHelpers.ParseExtensionsFrom(fileFilter)).Shorten(extensionsDisplayLength, eStringShortening.TakeFromEnd);
                return Invalid($"{path}\r\nIndicated file path does not use one of the allowed file extensions:\r\n{extensions}");
            }

            string expandedPath = GetFullyExpandedPath(path, fixedRootPath);
            bool existsAsFile = File.Exists(expandedPath);
            bool existsAsDirectory = Directory.Exists(expandedPath);

            // A path that is there as the other kind of thing is wrong whether or not missing paths are allowed
            if (pathType == ePathType.File && existsAsDirectory)
            {
                return Invalid("Looking for a file, path is an existing directory");
            }

            if (pathType == ePathType.Folder && existsAsFile)
            {
                return Invalid("Looking for a directory, path is an existing file");
            }

            bool doesPathExist = (pathType == ePathType.File) ? existsAsFile : existsAsDirectory;
            if (!doesPathExist && treatNonExistentPathAsInvalid)
            {
                return new Evaluation(IsPathValid: false, DoesPathExist: false, InvalidPathReason: $"{pathType} does not exist");
            }

            return new Evaluation(IsPathValid: true, DoesPathExist: doesPathExist, InvalidPathReason: null);
        }

        /// <summary>
        /// <paramref name="path"/> as a rooted path: a relative path is taken to be under <paramref name="fixedRootPath"/>,
        /// and anything else is left as it is
        /// </summary>
        public static string GetFullyExpandedPath (string path, string fixedRootPath)
        {
            if (fixedRootPath.IsNullOrEmpty() || Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.Combine(fixedRootPath, path);
        }

        /// <summary>
        /// <paramref name="path"/> made relative to <paramref name="fixedRootPath"/>, for a control that keeps its path
        /// short. Returns false, and leaves the path as it is, when it isn't under the fixed root, or is the root itself
        /// (which shortened to nothing would read as no path at all).
        /// </summary>
        public static bool TryShortenToFixedRoot (string path, string fixedRootPath, out string shortenedPath)
        {
            if (TryGetPartBelowFixedRoot(path, fixedRootPath, out string partBelow) && partBelow.Length > 0)
            {
                shortenedPath = partBelow;
                return true;
            }

            shortenedPath = path;
            return false;
        }

        /// <summary>
        /// The folder browsing should start in: the first of <paramref name="fixedRootPath"/>, then the selected path's
        /// folder (or <paramref name="initialBrowseRoot"/> while nothing is selected), that is a folder that is there.
        /// Null when none of them is, which leaves it to the picker.
        /// </summary>
        public static string FindBrowseStartFolder (string selectedPath, ePathType pathType, string initialBrowseRoot, string fixedRootPath)
        {
            string selectedFolder = initialBrowseRoot;
            if (selectedPath.IsNotNullOrEmpty())
            {
                string expandedPath = GetFullyExpandedPath(selectedPath, fixedRootPath);
                selectedFolder = (pathType == ePathType.File) ? Path.GetDirectoryName(expandedPath) : expandedPath;
            }

            foreach (string candidate in new[] { fixedRootPath, selectedFolder }.Where(_candidate => _candidate.IsNotNullOrEmpty()))
            {
                if (TryGetExistingFolder(candidate, out string folder))
                {
                    return folder;
                }
            }

            return null;
        }

        /// <summary>
        /// The folder above <paramref name="path"/>, for stepping up out of a folder from the keyboard. A relative path is
        /// taken to be under <paramref name="fixedRootPath"/>. Returns false when there is no path, or nothing above it.
        /// </summary>
        public static bool TryGetParentFolder (string path, string fixedRootPath, out string parentFolder)
        {
            parentFolder = null;
            if (path.IsNullOrEmpty())
            {
                return false;
            }

            try
            {
                DirectoryInfo parent = new DirectoryInfo(GetFullyExpandedPath(path, fixedRootPath)).Parent;
                parentFolder = parent?.FullName;
            }
            catch (Exception e) when (IsNotAPathException(e))
            {
            }

            return parentFolder != null;
        }

        // ===========[ Helper Methods ]========================================
        private static Evaluation Invalid (string reason) => new Evaluation(IsPathValid: false, DoesPathExist: false, InvalidPathReason: reason);

        // No filter at all takes any file, as an any file filter does
        private static bool HasAllowedExtension (string path, string fileFilter)
        {
            return fileFilter.IsNullOrEmpty() || PathHelpers.FindMatchingExtensionsFromFilter(path, fileFilter).Any();
        }

        // What follows the fixed root in a path under it, with no separator in front; empty for the root itself. Windows and
        //  macOS, where a typed path is ever checked, ignore case in a path, the same in every culture. The root's own name
        //  with more after it (c:\root-old against c:\root) is a folder beside the root, so what follows the root has to
        //  start a new part of the path.
        private static bool TryGetPartBelowFixedRoot (string path, string fixedRootPath, out string partBelow)
        {
            partBelow = null;
            if (path.IsNullOrEmpty()
                || fixedRootPath.IsNullOrEmpty()
                || !path.StartsWith(fixedRootPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string rest = path.Substring(fixedRootPath.Length);
            bool rootEndsWithSeparator = Array.IndexOf(kSeparators, fixedRootPath[fixedRootPath.Length - 1]) != -1;
            if ((rest.Length > 0) && !rootEndsWithSeparator && (Array.IndexOf(kSeparators, rest[0]) == -1))
            {
                return false;
            }

            partBelow = rest.TrimStart(kSeparators);
            return true;
        }

        // Text that isn't a path at all (a stray null character) can make the path APIs throw, which only means it isn't
        //  a path to go anywhere from
        private static bool IsNotAPathException (Exception e) => (e is ArgumentException) || (e is NotSupportedException) || (e is PathTooLongException);

        private static bool TryGetExistingFolder (string path, out string folder)
        {
            try
            {
                folder = Path.GetFullPath(path);
                if (Directory.Exists(folder))
                {
                    return true;
                }
            }
            catch (Exception e) when (IsNotAPathException(e))
            {
            }

            folder = null;
            return false;
        }

        // ===========[ Subclasses/structs ]====================================
        /// <summary>What <see cref="Evaluate"/> decided about a path</summary>
        /// <param name="IsPathValid">Whether the path can be used: an unset path counts, unless it's to be treated as invalid</param>
        /// <param name="DoesPathExist">Whether the path is there, as the kind of thing it's meant to name</param>
        /// <param name="InvalidPathReason">Why the path can't be used, or null when it can</param>
        public readonly record struct Evaluation (bool IsPathValid, bool DoesPathExist, string InvalidPathReason);
    }
}
