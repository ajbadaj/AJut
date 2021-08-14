namespace AJut.IO
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

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
    }
}
