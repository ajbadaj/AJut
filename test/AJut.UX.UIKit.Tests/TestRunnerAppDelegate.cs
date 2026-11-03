namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.ExceptionServices;
    using System.Text;
    using System.Threading.Tasks;
    using CoreGraphics;
    using Foundation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using UIKit;

    /// <summary>
    /// Runs every MSTest [TestMethod] in this assembly on the main thread when the app launches, prints each result,
    /// writes them all to <see cref="kResultsFileName"/> in the app's Documents folder, and quits. Views have to be made
    /// and driven on the main thread, so tests run there; a test that returns a Task is awaited, which lets the main run
    /// loop do any work the test queued (a layout pass, say) before the test carries on.
    /// </summary>
    /// <remarks>
    /// This is not the MSTest runner, only enough of it for these tests: [TestClass], [TestMethod], [TestInitialize],
    /// [TestCleanup], and [Ignore], the same as the Android test app's. Launch with the environment variable
    /// <see cref="kFilterVariable"/> set (simctl passes it on from SIMCTL_CHILD_AJUT_TEST_FILTER) to run only the tests
    /// whose "Class.Method" name contains it.
    /// </remarks>
    [Register(nameof(TestRunnerAppDelegate))]
    public sealed class TestRunnerAppDelegate : UIApplicationDelegate
    {
        /// <summary>The environment variable a run is filtered by.</summary>
        public const string kFilterVariable = "AJUT_TEST_FILTER";

        /// <summary>The file in the app's Documents folder the results are written to, which run-uikit-tests.sh reads.</summary>
        public const string kResultsFileName = "ajut-test-results.txt";

        // The runner finds and makes the test classes by reflection, which the trimmer can't follow. They are this app's
        // own types, and an app's own assembly is kept whole.
        private const string kOwnTestsAreKept = "The runner only reflects over this app's own test classes, and an app's own assembly is never trimmed.";

        // ===========[ Public Interface Methods ]===================================
        public override bool FinishedLaunching (UIApplication application, NSDictionary launchOptions) => true;

        // Matches the configuration Info.plist declares, which names TestRunnerSceneDelegate as the scene's delegate. An app
        // built with an SDK after iOS 26's has to use scenes, so the runner does too.
        public override UISceneConfiguration GetConfiguration (UIApplication application, UISceneSession connectingSceneSession, UISceneConnectionOptions options)
        {
            return new UISceneConfiguration("Default Configuration", connectingSceneSession.Role);
        }

        /// <summary>Runs the tests in <paramref name="window"/>, which the scene delegate has made visible</summary>
        internal static void BeginRun (UIWindow window)
        {
            // A test area views can be put in (some only listen while in a window), filling the window
            var controller = new UIViewController();
            controller.View.BackgroundColor = UIColor.SystemBackground;
            var testArea = new UIView(controller.View.Bounds)
            {
                AutoresizingMask = UIViewAutoresizing.FlexibleDimensions,
            };

            controller.View.AddSubview(testArea);
            window.RootViewController = controller;
            window.MakeKeyAndVisible();
            TestHost.TestArea = testArea;

            string filter = Environment.GetEnvironmentVariable(kFilterVariable);
            _ = RunAllAndReportAsync(string.IsNullOrEmpty(filter) ? null : filter);
        }

        // ===========[ Helper Methods ]===================================
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = kOwnTestsAreKept)]
        private static async Task RunAllAndReportAsync (string filter)
        {
            int passed = 0;
            int failed = 0;
            int ignored = 0;
            var report = new StringBuilder();

            // The window is made visible above, but lays out on the run loop's next turn
            await Task.Yield();

            try
            {
                foreach (Type testClass in FindTestClasses())
                {
                    MethodInfo[] methods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                    MethodInfo initialize = methods.FirstOrDefault(m => m.IsDefined(typeof(TestInitializeAttribute)));
                    MethodInfo cleanup = methods.FirstOrDefault(m => m.IsDefined(typeof(TestCleanupAttribute)));

                    foreach (MethodInfo test in methods.Where(m => m.IsDefined(typeof(TestMethodAttribute))))
                    {
                        string name = $"{testClass.Name}.{test.Name}";
                        if ((filter != null) && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (test.IsDefined(typeof(IgnoreAttribute)))
                        {
                            ++ignored;
                            Report(report, $"SKIP {name}");
                            continue;
                        }

                        string failure = await RunOneAsync(testClass, initialize, test, cleanup);
                        if (failure == null)
                        {
                            ++passed;
                            Report(report, $"PASS {name}");
                        }
                        else
                        {
                            ++failed;
                            Report(report, $"FAIL {name}: {failure}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ++failed;
                Report(report, $"FAIL runner: {ex}");
            }

            // The run script waits for this exact line, so keep its shape
            Report(report, $"DONE passed={passed} failed={failed} ignored={ignored}");
            WriteResultsFile(report.ToString());

            // A test app has nothing to do once its run is reported, and the run script waits for it to quit
            Environment.Exit(failed > 0 ? 1 : 0);
        }

        private static void Report (StringBuilder report, string line)
        {
            Console.WriteLine(line);
            report.AppendLine(line);
        }

        private static void WriteResultsFile (string results)
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            File.WriteAllText(Path.Combine(documents, kResultsFileName), results);
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = kOwnTestsAreKept)]
        private static IEnumerable<Type> FindTestClasses ()
        {
            return typeof(TestRunnerAppDelegate).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsDefined(typeof(TestClassAttribute)))
                .OrderBy(t => t.Name, StringComparer.Ordinal);
        }

        // Returns null on a pass, or the failure text
        [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = kOwnTestsAreKept)]
        private static async Task<string> RunOneAsync (Type testClass, MethodInfo initialize, MethodInfo test, MethodInfo cleanup)
        {
            object instance = null;
            try
            {
                instance = Activator.CreateInstance(testClass);
                await InvokeAsync(instance, initialize);
                await InvokeAsync(instance, test);
                return null;
            }
            catch (Exception ex)
            {
                return (ex is AssertFailedException or AssertInconclusiveException) ? ex.Message : ex.ToString();
            }
            finally
            {
                if (instance != null)
                {
                    try
                    {
                        await InvokeAsync(instance, cleanup);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Cleanup threw in {testClass.Name}: {ex}");
                    }
                }
            }
        }

        // Invokes the method and awaits it if it returned a task, rethrowing what the test itself threw rather than the
        // reflection wrapper around it
        private static async Task InvokeAsync (object instance, MethodInfo method)
        {
            if (method == null)
            {
                return;
            }

            object result;
            try
            {
                result = method.Invoke(instance, null);
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
                throw;
            }

            if (result is Task task)
            {
                await task;
            }
        }
    }

    /// <summary>What a test needs from the app hosting it.</summary>
    public static class TestHost
    {
        /// <summary>Where <see cref="Attach"/> puts a view: the whole window, under the status bar's safe area or not.</summary>
        public static UIView TestArea { get; internal set; }

        /// <summary>
        /// Puts <paramref name="view"/> in the app's window, filling the test area, for a view that only listens (or only
        /// shows rows) while it is in a window, and lays it out. Dispose the result to take it out again.
        /// </summary>
        public static IDisposable Attach (UIView view)
        {
            UIView area = TestHost.TestArea ?? throw new InvalidOperationException("Tests only run inside the test app.");
            view.Frame = area.Bounds;
            view.AutoresizingMask = UIViewAutoresizing.FlexibleDimensions;
            area.AddSubview(view);
            view.LayoutIfNeeded();
            return new AttachedView(view);
        }

        /// <summary>Lets the main run loop take one turn, then lays <paramref name="view"/> out, for work UIKit queues</summary>
        public static async Task LayOutAsync (UIView view)
        {
            await Task.Yield();
            view.SetNeedsLayout();
            view.LayoutIfNeeded();
        }

        /// <summary>Every view under <paramref name="view"/>, depth first, of type <typeparamref name="T"/></summary>
        public static IEnumerable<T> Descendants<T> (UIView view) where T : UIView
        {
            foreach (UIView child in view.Subviews)
            {
                if (child is T match)
                {
                    yield return match;
                }

                foreach (T descendant in Descendants<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        private sealed class AttachedView : IDisposable
        {
            private readonly UIView m_view;

            public AttachedView (UIView view)
            {
                m_view = view;
            }

            public void Dispose () => m_view.RemoveFromSuperview();
        }
    }
}
