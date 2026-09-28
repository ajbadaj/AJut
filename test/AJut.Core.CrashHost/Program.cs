namespace AJut.Core.CrashHost
{
    using System;
    using System.Threading;
    using AJut;

    /// <summary>
    /// Logs the <see cref="CrashHostScript"/> lines into a fresh log file, then dies the way it was told to.
    /// Args: {<see cref="eCrashHostDeath"/>} {<see cref="eLogFlushMode"/>} {log directory}
    /// </summary>
    internal static class Program
    {
        private static int Main (string[] args)
        {
            CrashHostNatives.SuppressCrashDialogs();

            eCrashHostDeath death = Enum.Parse<eCrashHostDeath>(args[0]);
            Logger.ShouldLogToConsole = false;
            Logger.ShouldLogToTrace = false;
            Logger.FlushMode = Enum.Parse<eLogFlushMode>(args[1]);
            Logger.CreateAndStartWritingToLogFileIn(args[2]);

            for (int index = 0; index < CrashHostScript.kBurstLineCount; ++index)
            {
                Logger.LogInfo($"{CrashHostScript.kBurstTag} {index}");
            }

            Logger.LogError(CrashHostScript.kErrorTag);

            for (int index = 0; index < CrashHostScript.kTrailingLineCount; ++index)
            {
                Logger.LogInfo($"{CrashHostScript.kTrailingTag} {index}");
            }

            Logger.LogInfo(CrashHostScript.kLastTag);

            Die(death);

            // Getting here means the death didn't take, which the tests read off the exit code
            return 0;
        }

        private static void Die (eCrashHostDeath death)
        {
            switch (death)
            {
                case eCrashHostDeath.FailFast:
                    Environment.FailFast("Crash host failing fast on purpose");
                    break;

                case eCrashHostDeath.StackOverflow:
                    RecurseForever(0);
                    break;

                case eCrashHostDeath.UnhandledException:
                    var thrower = new Thread(ThrowUnhandled);
                    thrower.Start();
                    thrower.Join();
                    break;

                case eCrashHostDeath.AccessViolation:
                    CrashHostNatives.StartThreadThatAccessViolates();
                    Thread.Sleep(Timeout.Infinite);
                    break;
            }
        }

        // The + 1 after the call keeps it from being turned into a loop
        private static int RecurseForever (int depth) => RecurseForever(depth + 1) + 1;

        private static void ThrowUnhandled ()
        {
            throw new InvalidOperationException("Crash host throwing unhandled on purpose");
        }
    }
}
