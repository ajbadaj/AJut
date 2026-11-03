namespace AJut.IO
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    public enum eFileCopyResolution { AcceptSource, AcceptDestination };
    public delegate eFileCopyResolution ResolveFileConflict (string sourcePath, string destinationPath);

    public static class DirectoryHelpers
    {
        public static Task<bool> Delete (string directoryTarget, int retryWaitMS = 33, int numRetries = 10)
        {
            return Delete(new DirectoryInfo(directoryTarget), retryWaitMS, numRetries);
        }

        public static async Task<bool> Delete (DirectoryInfo target, int retryWaitMS = 33, int numRetries = 10)
        {
            for (int retryCount = 0; retryCount < numRetries; ++retryCount)
            {
                try
                {
                    target.Refresh();
                    if (target.Exists)
                    {
                        target.Delete(recursive:true);
                        target.Refresh();
                        return target.Exists;
                    }
                }
                catch (IOException)
                {
                    await Task.Delay(retryWaitMS);
                }
                catch (UnauthorizedAccessException)
                {
                    await Task.Delay(retryWaitMS);
                }
            }

            target.Refresh();
            return target.Exists;
        }


        /// <summary>
        /// This utility manages moving, and moving + merging (if the desitination directory already exists) of the source directory and all 
        /// subdirectories (and files) into the destiation. By comparison  the utility <see cref="Directory.Move"/> allows only moving an
        /// existing directory, to a non-existant directory which it creates - meaning it does NOT handle merging.
        /// </summary>
        /// <param name="directorySourcePath">The source directory to move</param>
        /// <param name="directoryDestinationPath">The destiation directory to move the source to</param>
        /// <param name="fileCopyConflictResolver">The file conflict resolver, by default this will resolve conflict by <see cref="eFileCopyResolution.AcceptSource"/> - defaulting to overwrite destiation</param>
        public static void MoveAndMergeDirectory (string directorySourcePath, string directoryDestinationPath, ResolveFileConflict fileCopyConflictResolver = null)
        {
            // Don't accidentally move onto yourself
            if (IsSameFolder(directorySourcePath, directoryDestinationPath))
            {
                return;
            }

            CopyOrMoveAndMergeRecursively(Directory.Move, File.Move, directorySourcePath, directoryDestinationPath, fileCopyConflictResolver);
        }

        /// <summary>
        /// This utility manages copying + merging (if destination directory already exists) the source directory and all subdirectories into the desitination directory.
        /// </summary>
        /// <param name="directorySourcePath">The source directory to move</param>
        /// <param name="directoryDestinationPath">The destiation directory to move the source to</param>
        /// <param name="fileCopyConflictResolver">The file conflict resolver, by default this will resolve conflict by <see cref="eFileCopyResolution.AcceptSource"/> - defaulting to overwrite destiation</param>
        public static void CopyAndMergeDirectory (string directorySourcePath, string directoryDestinationPath, ResolveFileConflict fileCopyConflictResolver = null)
        {
            // Don't accidentally copy onto yourself
            if (IsSameFolder(directorySourcePath, directoryDestinationPath))
            {
                return;
            }

            CopyOrMoveAndMergeRecursively(null, File.Copy, directorySourcePath, directoryDestinationPath, fileCopyConflictResolver);
        }

        /// <summary>
        /// Whether <paramref name="sourcePath"/> and <paramref name="destinationPath"/> are one folder on disk, which a move or
        /// copy must never merge onto itself
        /// </summary>
        private static bool IsSameFolder (string sourcePath, string destinationPath)
        {
            // Merging a folder onto itself deletes each of its files (as the destination's) before moving or copying it (as
            //  the source's), so here the cost of a wrong answer is lopsided. Saying two folders are one when they aren't
            //  only does nothing; saying one folder is two loses its files.
            //
            //  Paths that match exactly are one folder, and paths that differ by more than case are two. Paths that differ
            //  only by case are the volume's call, not the platform's: the platform's rule is only its default, and a volume
            //  can differ from it. Android's shared storage (/storage/emulated) and FAT or exFAT cards ignore case, though
            //  Android tells case apart, and a Mac can have a case-sensitive APFS volume, though macOS ignores case. So the
            //  volume is asked, and its answer decides. When it can't be asked, the platform's rule decides.
            string source = PathHelpers.NormalizePath(sourcePath, ignoreCase: false);
            string destination = PathHelpers.NormalizePath(destinationPath, ignoreCase: false);
            if (source.Equals(destination, StringComparison.Ordinal))
            {
                return true;
            }

            if (!source.Equals(destination, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return DoesVolumeIgnoreCaseAt(sourcePath) ?? PathPlatform.IgnoresCase;
        }

        /// <summary>
        /// Whether the volume holding the existing folder <paramref name="folderPath"/> ignores case in its names, or null
        /// when that can't be found out here (the folder doesn't exist, or no part of its path has a letter)
        /// </summary>
        private static bool? DoesVolumeIgnoreCaseAt (string folderPath)
        {
            // The folder is asked for under another case: the deepest part of its path that has a letter, with each letter's
            //  case flipped (Photos becomes pHOTOS). Only a volume that ignores case finds it, short of a real folder that
            //  happens to be named exactly that, and taking one for a volume that ignores case only does nothing (above).
            //  The deepest part with a letter is used, since the volume that matters is the one the folder itself is on, and
            //  a path can cross from one volume to another higher up: on Android, /storage/emulated/0 is shared storage that
            //  ignores case, inside a file system that tells case apart.
            string current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath));
            if (!Directory.Exists(current))
            {
                return null;
            }

            while (current != null)
            {
                string name = Path.GetFileName(current);
                string flippedName = _FlipCase(name);
                string parent = Path.GetDirectoryName(current);
                if ((parent != null) && (flippedName != name))
                {
                    return Directory.Exists(Path.Combine(parent, flippedName));
                }

                current = parent;
            }

            return null;

            static string _FlipCase (string _name)
            {
                char[] _flipped = _name.ToCharArray();
                for (int _index = 0; _index < _flipped.Length; ++_index)
                {
                    _flipped[_index] = Char.IsUpper(_flipped[_index]) ? Char.ToLowerInvariant(_flipped[_index]) : Char.ToUpperInvariant(_flipped[_index]);
                }

                return new string(_flipped);
            }
        }

        private static void CopyOrMoveAndMergeRecursively (Action<string, string> directoryCreationShortcut, Action<string, string> fileAction, string directorySourcePath, string directoryDestinationPath, ResolveFileConflict fileCopyConflictResolver)
        {
            // Step 1) Generate the directory if it doesn't exist
            if (!Directory.Exists(directoryDestinationPath))
            {
                // If we have a generation shortcut, use that - this will end the operation as no merging is needed
                if (directoryCreationShortcut != null)
                {
                    directoryCreationShortcut(directorySourcePath, directoryDestinationPath);
                    return;
                }
                // If we don't then create the directory
                else
                {
                    Directory.CreateDirectory(directoryDestinationPath);
                }
            }

            // Step 2) Go over all files in this directory and copy/move them in, resolving merges as needed
            fileCopyConflictResolver = fileCopyConflictResolver ?? _DefaultResolution;
            foreach (string sourceFilePath in Directory.GetFiles(directorySourcePath))
            {
                string destinationFilePath = Path.Combine(directoryDestinationPath, Path.GetRelativePath(directorySourcePath, sourceFilePath));
                eFileCopyResolution action = eFileCopyResolution.AcceptSource;
                if (File.Exists(destinationFilePath))
                {
                    action = fileCopyConflictResolver(sourceFilePath, destinationFilePath);
                }

                if (action == eFileCopyResolution.AcceptSource)
                {
                    File.Delete(destinationFilePath);
                    fileAction(sourceFilePath, destinationFilePath);
                }
            }

            // Step 3) Copy/Move and merge in subdirectories and their files by recursing
            foreach (string sourceDirectoryChild in Directory.GetDirectories(directorySourcePath))
            {
                // The subfolder's own name, under the destination. GetDirectoryName would give its parent, the source itself.
                string destinationDirectoryChild = Path.Combine(directoryDestinationPath, Path.GetFileName(sourceDirectoryChild));
                CopyOrMoveAndMergeRecursively(directoryCreationShortcut, fileAction, sourceDirectoryChild, destinationDirectoryChild, fileCopyConflictResolver);
            }

            eFileCopyResolution _DefaultResolution (string _src, string _dst) => eFileCopyResolution.AcceptSource;
        }
    }
}
