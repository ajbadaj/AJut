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
            if (PathHelpers.ArePathsMatching(directorySourcePath, directoryDestinationPath))
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
            if (PathHelpers.ArePathsMatching(directorySourcePath, directoryDestinationPath))
            {
                return;
            }

            CopyOrMoveAndMergeRecursively(null, File.Copy, directorySourcePath, directoryDestinationPath, fileCopyConflictResolver);
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
                string destinationDirectoryChild = Path.Combine(directoryDestinationPath, Path.GetDirectoryName(sourceDirectoryChild));
                CopyOrMoveAndMergeRecursively(directoryCreationShortcut, fileAction, sourceDirectoryChild, destinationDirectoryChild, fileCopyConflictResolver);
            }

            eFileCopyResolution _DefaultResolution (string _src, string _dst) => eFileCopyResolution.AcceptSource;
        }
    }
}
