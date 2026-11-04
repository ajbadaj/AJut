namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.ExceptionServices;
    using System.Text;
    using System.Threading.Tasks;
    using Android.App;
    using Android.Content;
    using Android.OS;
    using Android.Util;
    using Android.Views;
    using Android.Widget;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Runs every MSTest [TestMethod] in this assembly on the main thread when the app launches, and logs each
    /// result under <see cref="kLogTag"/>. Views have to be created and driven on the main thread, so tests run
    /// there; a test that returns a Task is awaited, which lets the main looper run any work the test posted
    /// to it before the test carries on.
    /// </summary>
    /// <remarks>
    /// This is not the MSTest runner, only enough of it for these tests: [TestClass], [TestMethod],
    /// [TestInitialize], [TestCleanup] and [Ignore]. Launch with the string extra "filter" to run only the tests
    /// whose "Class.Method" name contains it.
    /// </remarks>
    [Activity(Name = "dev.ajut.ux.android.tests.TestRunnerActivity", Label = "AJut Android Tests", MainLauncher = true, Exported = true)]
    public sealed class TestRunnerActivity : Activity
    {
        /// <summary>The logcat tag every result is written under.</summary>
        public const string kLogTag = "AJutTests";

        private TextView? m_output;

        // ===========[ Setup/Construction/Teardown ]===================================
        protected override void OnCreate (Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            TestHost.Context = this;

            // A test area views can be attached to (some only listen while in a window), above the results.
            var testArea = new FrameLayout(this);
            TestHost.TestArea = testArea;
            m_output = new TextView(this);
            var scroll = new ScrollView(this);
            scroll.AddView(m_output);
            var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
            root.AddView(testArea, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
            root.AddView(scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));
            this.SetContentView(root);

            string? filter = this.Intent?.GetStringExtra("filter");
            _ = this.RunAllAndReportAsync(filter);
        }

        protected override void OnDestroy ()
        {
            TestHost.Context = null;
            TestHost.TestArea = null;
            base.OnDestroy();
        }

        // ===========[ Helper Methods ]===================================
        private async Task RunAllAndReportAsync (string? filter)
        {
            int passed = 0;
            int failed = 0;
            int ignored = 0;
            var report = new StringBuilder();

            // OnCreate runs before the activity's window is attached, and some tests need their views in a window, so
            // wait for it (the looper keeps running meanwhile) before the first test.
            const int kWindowWaitSeconds = 10;
            const int kWindowPollMilliseconds = 20;
            DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(kWindowWaitSeconds);
            while (!(TestHost.TestArea?.IsAttachedToWindow ?? false) && (DateTime.UtcNow < deadline))
            {
                await Task.Delay(kWindowPollMilliseconds);
            }

            try
            {
                foreach (Type testClass in _FindTestClasses())
                {
                    MethodInfo[] methods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                    MethodInfo? initialize = methods.FirstOrDefault(m => m.IsDefined(typeof(TestInitializeAttribute)));
                    MethodInfo? cleanup = methods.FirstOrDefault(m => m.IsDefined(typeof(TestCleanupAttribute)));

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
                            this.Report(report, $"SKIP {name}");
                            continue;
                        }

                        string? failure = await _RunOneAsync(testClass, initialize, test, cleanup);
                        if (failure == null)
                        {
                            ++passed;
                            this.Report(report, $"PASS {name}");
                        }
                        else
                        {
                            ++failed;
                            this.Report(report, $"FAIL {name}: {failure}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ++failed;
                this.Report(report, $"FAIL runner: {ex}");
            }

            // The run script waits for this exact line, so keep its shape.
            this.Report(report, $"DONE passed={passed} failed={failed} ignored={ignored}");
        }

        private void Report (StringBuilder report, string line)
        {
            Log.Info(kLogTag, line);
            report.AppendLine(line);
            if (m_output != null)
            {
                m_output.Text = report.ToString();
            }
        }

        private static IEnumerable<Type> _FindTestClasses ()
        {
            return typeof(TestRunnerActivity).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsDefined(typeof(TestClassAttribute)))
                .OrderBy(t => t.Name, StringComparer.Ordinal);
        }

        // Returns null on a pass, or the failure text.
        private static async Task<string?> _RunOneAsync (Type testClass, MethodInfo? initialize, MethodInfo test, MethodInfo? cleanup)
        {
            object? instance = null;
            try
            {
                instance = Activator.CreateInstance(testClass);
                await _InvokeAsync(instance, initialize);
                await _InvokeAsync(instance, test);
                return null;
            }
            catch (Exception ex)
            {
                return ex is AssertFailedException or AssertInconclusiveException
                    ? ex.Message
                    : ex.ToString();
            }
            finally
            {
                if (instance != null)
                {
                    try
                    {
                        await _InvokeAsync(instance, cleanup);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(kLogTag, $"Cleanup threw in {testClass.Name}: {ex}");
                    }
                }
            }
        }

        // Invokes the method and awaits it if it returned a task, rethrowing what the test itself threw rather
        // than the reflection wrapper around it.
        private static async Task _InvokeAsync (object? instance, MethodInfo? method)
        {
            if (method == null)
            {
                return;
            }

            object? result;
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
        /// <summary>The running test activity, for creating Views. Set for the whole run.</summary>
        public static Context? Context { get; internal set; }

        /// <summary>The test activity as a non-null context; a test only runs while it is set.</summary>
        public static Context RequiredContext => TestHost.Context ?? throw new InvalidOperationException("Tests only run inside TestRunnerActivity.");

        /// <summary>Where <see cref="Attach"/> puts a view.</summary>
        public static FrameLayout? TestArea { get; internal set; }

        /// <summary>
        /// Puts <paramref name="view"/> in the activity's window, for a view that only listens while attached. Dispose
        /// the result to take it out again.
        /// </summary>
        public static IDisposable Attach (View view)
        {
            FrameLayout area = TestHost.TestArea ?? throw new InvalidOperationException("Tests only run inside TestRunnerActivity.");
            area.AddView(view, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
            return new AttachedView(area, view);
        }

        private sealed class AttachedView : IDisposable
        {
            private readonly FrameLayout m_area;
            private readonly View m_view;

            public AttachedView (FrameLayout area, View view)
            {
                m_area = area;
                m_view = view;
            }

            public void Dispose () => m_area.RemoveView(m_view);
        }
    }
}
