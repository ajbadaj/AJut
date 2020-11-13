namespace AJut.Application.SourceControl
{
    using AJut.IO;
    using AJut.Text;
    using System;
    using System.IO;

    public static class PerforceCmdManager
    {
        private static WorkspaceToChekcoutCache g_cache;
        private static bool g_saveCacheAfterEachOperation;
        private static string g_cacheRoot;

        public static PerforceConnection Connection { get; private set; }

        static PerforceCmdManager()
        {
            Connection = new PerforceConnection();
        }

        /// <summary>
        /// Setup the cache, and the connection if it's not set.
        /// </summary>
        /// <param name="cacheRoot">The place to ache files (or null if you don't want to cache)</param>
        /// <param name="saveCacheAfterEachOperation">
        /// Indicates if after every operation that adds to the cache, if the cache file on disk 
        /// should be updated (<c>true</c>) or not (<c>false</c>).
        /// </param>
        public static void SetupCache(string cacheRoot, bool saveCacheAfterEachOperation)
        {
            g_cacheRoot = cacheRoot;
            g_saveCacheAfterEachOperation = saveCacheAfterEachOperation;

            if (!Connection.IsConnected || Connection.WorkspaceName == null)
            {
                ReEstablishConnection();
            }
        }

        /// <summary>
        /// Outputs the cache (if any) to disk
        /// </summary>
        public static void PushCacheToDisk()
        {
            if (g_cache != null)
            {
                g_cache.Write();
            }
        }

        public static readonly ProcConfiguration kP4 = new ProcConfiguration("p4");

        /// <summary>
        /// Opens the file to be written, and adds the file to source control (for either add or checkout). Structured to match <see cref="AJut.IO.OpenForWriteFunction"/>;.
        /// </summary>
        /// <param name="filePath">Path of the file to open</param>
        /// <returns>FileStream to write to.</returns>
        public static FileStream OpenAndAddOrCheckoutFile(string filePath)
        {
            try
            {
                filePath = PathHelpers.Normalize(filePath);
                FileInfo target = new FileInfo(filePath);

                if (!Connection.IsConnected)
                {
                    ReEstablishConnection();
                }

                if (Connection.IsConnected)
                {
                    // It's something that needs to be (potentially) checked out
                    if (target.Exists)
                    {
                        // It's likely under source control
                        if (target.IsReadOnly)
                        {
                            Logger.LogInfo($"Attempting to checkout '{filePath}' from perforce.");

                            // Try to check it out
                            var p4Result = kP4.BuildRunner().Run($"edit \"{filePath}\"");
                            if (p4Result.Success)
                            {
                                target.Refresh();
                                int numSleeps = 0;
                                // Wait up to 3 seconds
                                while (target.IsReadOnly && numSleeps < 15)
                                {
                                    ++numSleeps;
                                    System.Threading.Thread.Sleep(200);
                                    target.Refresh();
                                }

                                if (!target.IsReadOnly)
                                {
                                    Logger.LogInfo($"File checkout '{filePath}' was successfull!");
                                    return target.OpenWrite();
                                }
                                else
                                {
                                    Logger.LogError($"After successful file checkout of '{filePath}' file was still readonly!");
                                }
                            }
                            else
                            {
                                Logger.LogError($"Even though the service seemed connected, failed to checkout file ({filePath}) - Error report:\n{p4Result.GenerateFailureReport()}");
                            }

                            if (FileHelpers.SetReadOnlyAndWait(target, false))
                            {
                                if (g_cache != null)
                                {
                                    Logger.LogInfo($"File '{filePath}' checkout did not work, adding to 'check out later' cache");
                                    g_cache.ToCheckoutPaths.Add(filePath);

                                    if(g_saveCacheAfterEachOperation)
                                    {
                                        PushCacheToDisk();
                                    }
                                }

                                return target.OpenWrite();
                            }

                            // At this point we've tried everything, time to throw in the towel
                            Logger.LogError("After every attempt to check out, and to force writable, file '{0}' still is not writable! Unable to make any changes to file!!!");
                            return null;
                        }
                        // It's likely not under source control
                        else
                        {
                            return target.OpenWrite();
                        }
                    }
                    else
                    {
                        FileStream output = target.OpenWrite();

                        Logger.LogInfo($"Attempting to mark '{filePath}' for add in perforce.");

                        var p4Result = kP4.BuildRunner().Run("add \"{0}\"", filePath);
                        if (!p4Result.Success)
                        {
                            Logger.LogError($"Even though the service seemed connected, a file ({filePath}) was unable to be added to source control.\nReport: {p4Result.GenerateFailureReport()}");
                            if (g_cache != null)
                            {
                                Logger.LogInfo($"File '{filePath}' mark for add did not work, adding to 'mark for add later' cache");
                                g_cache.ToAddPaths.Add(filePath);

                                if (g_saveCacheAfterEachOperation)
                                {
                                    PushCacheToDisk();
                                }
                            }
                        }

                        return output;
                    }
                }

                if (g_cache != null)
                {
                    if (target.Exists)
                    {
                        if (target.IsReadOnly)
                        {
                            if (!FileHelpers.SetReadOnlyAndWait(target, false, 3000, 50))
                            {
                                return null;
                            }

                            Logger.LogInfo($"File '{filePath}' checkout did not work, adding to 'check out later' cache");
                            g_cache.ToCheckoutPaths.Add(filePath);
                            if (g_saveCacheAfterEachOperation)
                            {
                                PushCacheToDisk();
                            }

                            return target.OpenWrite();
                        }
                    }
                    else
                    {
                        var stream = target.OpenWrite();

                        Logger.LogInfo($"File '{filePath}' mark for add did not work, adding to 'mark for add later' cache");
                        g_cache.ToAddPaths.Add(filePath);
                        if (g_saveCacheAfterEachOperation)
                        {
                            PushCacheToDisk();
                        }

                        return stream;
                    }
                }
            }
            catch(Exception exc)
            {
                Logger.LogError($"PerforceCmdManager - Failed to open file ({filePath}) for writing", exc);
            }

            return null;
        }

        /// <summary>
        /// Attempt to re-establish the connection to p4
        /// </summary>
        public static void ReEstablishConnection()
        {
            Logger.LogInfo("Attempting to re-establish p4 connection from the PerforceCmdManager");
            try
            {
                var p4Result = kP4.BuildRunner(timeout: 2000).Run("set p4client");
                if (!p4Result.Success)
                {
                    return;
                }

                Connection.WorkspaceName = RegexHelper.Match(p4Result.OutputText, "=([\\w_]+)").GetMostSpecificCaptureText();
                if (Connection.WorkspaceName == null)
                {
                    Connection.IsConnected = false;
                    return;
                }

                p4Result = kP4.BuildRunner(timeout: 2000).Run("client -o {0}", Connection.WorkspaceName);
                if (p4Result.Success)
                {
                    Connection.WorkspaceRootPath = RegexHelper.Match(p4Result.OutputText, "^Root:\\s+([\\w:\\\\]+)").GetMostSpecificCaptureText();
                }

                Connection.IsConnected = true;
                ResetCache();
            }
            catch(Exception exc)
            {
                Logger.LogError("Error in PerforceCmdManager, re-establishing connection encountered exception!", exc);
            }
        }

        private static void ResetCache()
        {
            Logger.LogInfo("=======[Attempting to reset the PerforceCmdManager's check out/add later cache]=======");
            if (g_cache != null)
            {
                g_cache.Write();
                g_cache = null;
            }

            if (Connection.IsConnected && Connection.WorkspaceName != null)
            {
                g_cache = WorkspaceToChekcoutCache.Read(g_cacheRoot, Connection.WorkspaceName);
            }


            if (g_cache != null)
            {
                foreach (string file in g_cache.ToCheckoutPaths.ToArray())
                {
                    Logger.LogInfo($"Attempting to checkout file ({file}) which was asked to be checked out, but was unable to do so in the past.");
                    try
                    {
                        var editResult = kP4.BuildRunner().Run("edit \"{0}\"", file);
                        if (editResult.Success)
                        {
                            g_cache.ToCheckoutPaths.Remove(file);
                        }
                        else
                        {
                            Logger.LogError($"Failed to checkout file ({file}) again, checkout failure report:\n{editResult.GenerateFailureReport()}");
                        }

                    }
                    catch (Exception exc)
                    {
                        Logger.LogError($"Checkout of file ({file}) failed due to exception!", exc);
                    }
                }
                foreach (string file in g_cache.ToAddPaths.ToArray())
                {
                    Logger.LogInfo($"Attempting to add file ({file}) to source control which was asked to be added, but was unable to do so in the past.");
                    try
                    {
                        var addResult = kP4.BuildRunner().Run("add \"{0}\"", file);
                        if (addResult.Success)
                        {
                            g_cache.ToCheckoutPaths.Remove(file);
                        }
                        else
                        {
                            Logger.LogError($"Failed to add file ({file}) to source control again, checkout failure report:\n{addResult.GenerateFailureReport()}");
                        }

                    }
                    catch (Exception exc)
                    {
                        Logger.LogError($"Source control add of file ({file}) failed due to exception!", exc);
                    }
                }

                g_cache.Write();
            }
        }
    }
}
