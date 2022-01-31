namespace AJut.IO
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Threading.Tasks;
    using AJut;

    /// <summary>
    /// A function that takes in a path and returns a file stream for writing. Allows for <see cref="System.IO.File.OpenWrite"/> or any 
    /// similarly structured function that may perform additional operations before returning the file stream (ie source control operations)
    /// </summary>
    /// <param name="path">The path of the file to open</param>
    /// <returns>A <see cref="FileStream"/> (that can be null) that will be used in a write operation.</returns>
    public delegate FileStream OpenForWriteFunction(string path);

    public static class FileHelpers
    {
        public static bool TryOpenForWrite (string filePath, bool forceWritable, out FileStream outputStream)
        {
            if (filePath == null)
            {
                outputStream = null;
                return false;
            }
            try
            {
                FileInfo targetFile = new FileInfo(filePath);

                // Make sure the directory exists, otherwise the write will fail
                if (!targetFile.Directory.Exists)
                {
                    targetFile.Directory.Create();
                }

                if (forceWritable && targetFile.IsReadOnly)
                {
                    SetReadOnlyAndWait(targetFile, false);
                }

                if (targetFile.IsReadOnly)
                {
                    outputStream = null;
                    return false;
                }

                outputStream = targetFile.OpenWrite();
                return true;
            }
            catch (Exception exc)
            {
                Logger.LogError(exc);

                outputStream = null;
                return false;
            }
        }

        /// <summary>
        /// Sets the readonly flag to true, and waits until the value has changed (and therefore is writable)
        /// </summary>
        /// <returns><c>true</c> if the target file's readonly status matches the requested readonly, <c>false</c> otherwise.</returns>
        public static bool SetReadOnlyAndWait (FileInfo targetFile, bool readOnly, int waitMaxMS = 1000, int sleepIntervalMS = 25)
        {
            try
            {
                targetFile.Refresh();
                if (targetFile.IsReadOnly == readOnly)
                {
                    return true;
                }

                targetFile.IsReadOnly = readOnly;
                targetFile.Refresh();
                while (targetFile.IsReadOnly && waitMaxMS > 0)
                {
                    System.Threading.Thread.Sleep(sleepIntervalMS);
                    waitMaxMS -= sleepIntervalMS;
                    targetFile.Refresh();
                }

                return targetFile.IsReadOnly == readOnly;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the embedded resource name so that an embedded resource can be accessed by it's translated name.
        /// </summary>
        /// <param name="relativePath">The relative path to the embedded resource</param>
        /// <param name="assembly">The assembly that the resource is in, or null if you want to use the executing assembly.</param>
        /// <returns>The translated embedded resource name</returns>
        public static string GenerateEmbeddedResourceName (string relativePath, Assembly assembly = null)
        {
            assembly = assembly ?? Assembly.GetCallingAssembly();
            return assembly.GetName().Name + "." + relativePath.Replace('/', '.').Replace('\\', '.').TrimStart('.'); ;
        }

        /// <summary>
        /// Gets a <see cref="Stream"/> for the passed in embedded resource.
        /// </summary>
        /// <param name="relativePath">The relative path to the embedded resource</param>
        /// <param name="assembly">The assembly that the resource is in, or null if you want to use the calling assembly.</param>
        /// <returns>A stream for reading an embedded resource.</returns>
        public static Stream GetEmbeddedResourceStream (string relativePath, Assembly assembly = null)
        {
            assembly = assembly ?? Assembly.GetCallingAssembly();
            string resourcePath = GenerateEmbeddedResourceName(relativePath, assembly);
            return assembly.GetManifestResourceStream(resourcePath);
        }

        /// <summary>
        /// The text of the embedded resource.
        /// </summary>
        /// <param name="relativePath">The relative path to the embedded resource</param>
        /// <param name="assembly">The assembly that the resource is in, or null if you want to use the executing assembly.</param>
        /// <returns>All the text read from the indicated resource stream</returns>
        public static string ReadEmbeddedResourceText (string relativePath, Assembly assembly = null)
        {
            assembly = assembly ?? Assembly.GetCallingAssembly();
            using (Stream stream = GetEmbeddedResourceStream(relativePath, assembly))
            {
                StreamReader reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// [async] The text of the embedded resource.
        /// </summary>
        /// <param name="relativePath">The relative path to the embedded resource</param>
        /// <param name="assembly">The assembly that the resource is in, or null if you want to use the executing assembly.</param>
        /// <returns>All the text read from the indicated resource stream</returns>
        public static async Task<string> ReadEmbeddedResourceTextAsync (Assembly assembly, string relativePath)
        {
            assembly = assembly ?? Assembly.GetCallingAssembly();
            string resourcePath = GenerateEmbeddedResourceName(relativePath, assembly);
            using (Stream stream = assembly.GetManifestResourceStream(resourcePath))
            {
                if (stream == null)
                {
                    return null;
                }

                StreamReader reader = new StreamReader(stream);
                return await reader.ReadToEndAsync();
            }
        }

        /// <summary>
        /// Writes the full contents of a given stream to the file location
        /// </summary>
        /// <param name="filePath">The file to write to</param>
        /// <param name="stream">The stream to read and copy to the given file</param>
        public static void WriteStreamToFile (string filePath, Stream stream)
        {
            using (Stream writeStream = File.OpenWrite(filePath))
            {
                using (StreamWriter writer = new StreamWriter(writeStream))
                {
                    writer.Write(stream);
                }
            }
        }

        /// <summary>
        /// Get a readable and complete stream for a file at an absolute path, an embedded manifest resource (packed file), or a file from the interwebs
        /// </summary>
        /// <param name="fileUri">The uri of the file</param>
        /// <returns>The stream, up to the caller to dispose.</returns>
        public static Stream GetStreamForFileUri (Uri fileUri)
        {
            if (!fileUri.IsAbsoluteUri || fileUri.IsFile)
            {
                if (File.Exists(fileUri.OriginalString))
                {
                    return File.OpenRead(fileUri.OriginalString);
                }

                return null;
            }
            else if (fileUri.Scheme.Equals("pack", StringComparison.InvariantCultureIgnoreCase))
            {
                int stopInd = fileUri.AbsolutePath.IndexOf(';');
                string assemblyName = fileUri.AbsolutePath.Substring(0, stopInd).Trim('/');
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == assemblyName);
                string embeddedResourcePath = fileUri.AbsolutePath.Replace($"{assemblyName};component/", "").Trim('/');
                embeddedResourcePath = FileHelpers.GenerateEmbeddedResourceName(embeddedResourcePath, assembly);
                return assembly.GetManifestResourceStream(embeddedResourcePath);
            }
            else
            {
                var finalStream = new MemoryStream();
                using (HttpWebResponse response = (HttpWebResponse)WebRequest.Create(fileUri).GetResponse())
                {
                    using (BinaryReader reader = new BinaryReader(response.GetResponseStream()))
                    {
                        byte[] transferBuffer = reader.ReadBytes(1024);
                        while (transferBuffer.Length > 0)
                        {
                            finalStream.Write(transferBuffer, 0, transferBuffer.Length);
                            transferBuffer = reader.ReadBytes(1024);
                        }

                        return finalStream;
                    }
                }
            }
        }
    }
}
