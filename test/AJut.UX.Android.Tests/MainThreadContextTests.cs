namespace AJut.UX.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Android.OS;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// What a BindingSet made on the main thread relies on, checked on a real device: the main thread has a
    /// SynchronizationContext to capture, a post to it from a worker runs on the main looper, and a worker has none.
    /// The set tells the main thread apart by thread id, not by context instance, so the same-instance check is a
    /// fact about Android rather than something the set needs.
    /// </summary>
    [TestClass]
    public class MainThreadContextTests
    {
        [TestMethod]
        public void MainThread_HasOneStableSynchronizationContext ()
        {
            Assert.AreSame(Looper.MainLooper, Looper.MyLooper(), "Tests run on the main thread");
            SynchronizationContext? first = SynchronizationContext.Current;
            Assert.IsNotNull(first, "The main thread has a SynchronizationContext");
            Assert.AreSame(first, SynchronizationContext.Current, "Reading it twice gives the same instance");
        }

        [TestMethod]
        public async Task PostFromAWorker_RunsOnTheMainLooper ()
        {
            SynchronizationContext context = SynchronizationContext.Current!;
            var ranOnMain = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await Task.Run(() => context.Post(_ => ranOnMain.TrySetResult(Looper.MyLooper() == Looper.MainLooper), null));

            Task finished = await Task.WhenAny(ranOnMain.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.AreSame(ranOnMain.Task, finished, "The post never ran");
            Assert.IsTrue(ranOnMain.Task.Result);
        }

        [TestMethod]
        public async Task WorkerThread_HasNoContext ()
        {
            SynchronizationContext? onWorker = await Task.Run(() => SynchronizationContext.Current);
            Assert.IsNull(onWorker, "A BindingSet made off the main thread would capture nothing");
        }
    }
}
