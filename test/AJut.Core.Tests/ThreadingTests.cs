namespace AJut.Core.UnitTests
{
    using AJut.Threading;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class ThreadingTests
    {
        [TestMethod]
        public async Task ThreadWorker_ProofOfConcept ()
        {
            var finalOutput = new List<string>();
            var bkg = new ThreadWorker<int, string>();

            bkg.StartThreadLoop(_DoBkgWork);
            bkg.OutputResults.DataReceived += _OnOutputDataReceived;

            bkg.InputToProcess.Add(5);
            bkg.InputToProcess.Add(1088050);

            bkg.InputToProcess.Add(1010155001);

            // A graceful shutdown stops after the frame it's in, so the queue is finished first
            await bkg.WhenAllInputProcessingCompleted();
            Assert.IsTrue(await bkg.ShutdownGracefullyAndWaitForCompletion());
            bkg.OutputResults.DataReceived -= _OnOutputDataReceived;

            Assert.IsFalse(bkg.IsActive);
            string result = String.Join(",", finalOutput);
            Assert.AreEqual("5,1,88,5,1,1,155,1", result);

            void _DoBkgWork (ThreadWorkerDataTracker<int, TNone, string> data)
            {
                if (!data.InputToProcess.Any())
                {
                    return;
                }

                // Grab the input state
                int number = data.InputToProcess.TakeNext();

                // Process
                List<string> output = new List<string>();
                output.AddRange(number.ToString().Split(new[] { '0' }, StringSplitOptions.RemoveEmptyEntries));

                // Set the output state
                data.OutputResults.AddRange(output);
                data.OutputResults.NotifyDataReceived();
            }

            void _OnOutputDataReceived (object sender, EventArgs e)
            {
                finalOutput.AddRange(bkg.OutputResults.TakeAll());
            }
        }

        [TestMethod]
        public async Task ThreadWorker_CancellationToken ()
        {
            var finalOutput = new List<string>();
            var bkg = new ThreadWorker<int, string>();

            CancellationTokenSource cancellor = new CancellationTokenSource();
            bkg.StartThreadLoop(_DoBkgWork, cancellor.Token);
            bkg.OutputResults.DataReceived += _OnOutputDataReceived;

            bkg.InputToProcess.Add(5);
            bkg.InputToProcess.Add(1088050);
            await bkg.WhenAllInputProcessingCompleted();

            cancellor.Cancel();
            bkg.InputToProcess.Add(1010155001);
            Assert.IsTrue(await bkg.ShutdownGracefullyAndWaitForCompletion());
            bkg.OutputResults.DataReceived -= _OnOutputDataReceived;

            Assert.IsFalse(bkg.IsActive);
            string result = String.Join(",", finalOutput);
            Assert.AreEqual("5,1,88,5", result);

            void _DoBkgWork (ThreadWorkerDataTracker<int, TNone, string> data)
            {
                if (!data.InputToProcess.Any())
                {
                    return;
                }

                // Grab the input state
                int number = data.InputToProcess.TakeNext();

                // Process
                List<string> output = new List<string>();
                output.AddRange(number.ToString().Split(new[] { '0' }, StringSplitOptions.RemoveEmptyEntries));

                // Set the output state
                data.OutputResults.AddRange(output);
                data.OutputResults.NotifyDataReceived();
            }

            void _OnOutputDataReceived (object sender, EventArgs e)
            {
                finalOutput.AddRange(bkg.OutputResults.TakeAll());
            }
        }

        [TestMethod]
        public async Task ThreadWorker_ExecutionState_Test ()
        {
            var finalOutput = new List<string>();
            var bkg = new ThreadWorker<int, char, string>();

            // Split all incoming numbers on 0 & 5
            bkg.ExecutionState.Add('0');
            bkg.ExecutionState.Add('5');
            bkg.StartThreadLoop(_DoBkgWork);

            bkg.OutputResults.DataReceived += _OnOutputDataReceived;

            bkg.InputToProcess.Add(5);
            bkg.InputToProcess.Add(1088050);

            // A graceful shutdown stops after the frame it's in, so the queue is finished first
            await bkg.WhenAllInputProcessingCompleted();
            Assert.IsTrue(await bkg.ShutdownGracefullyAndWaitForCompletion());
            bkg.OutputResults.DataReceived -= _OnOutputDataReceived;

            Assert.IsFalse(bkg.IsActive);
            string result = String.Join(",", finalOutput);
            Assert.AreEqual("1,88", result);

            void _DoBkgWork (ThreadWorkerDataTracker<int, char, string> data)
            {
                if (!data.InputToProcess.Any())
                {
                    return;
                }

                // Grab the input state
                int number = data.InputToProcess.TakeNext();
                char[] splitOn = data.ExecutionState.ToArray();

                // Process
                List<string> output = new List<string>();
                output.AddRange(number.ToString().Split(splitOn, StringSplitOptions.RemoveEmptyEntries));

                // Set the output state
                data.OutputResults.AddRange(output);
                data.OutputResults.NotifyDataReceived();
            }

            void _OnOutputDataReceived (object sender, EventArgs e)
            {
                finalOutput.AddRange(bkg.OutputResults.TakeAll());
            }
        }

        [TestMethod]
        public async Task ThreadWorker_ShutdownThatTimesOut_ReturnsFalseAndTheLoopStillStops ()
        {
            const int kTimeoutMs = 1;
            var frameEntered = new ManualResetEventSlim();
            var releaseFrame = new ManualResetEventSlim();
            var bkg = new ThreadWorker<int, int>();
            bkg.StartThreadLoop(_HoldFrame);
            try
            {
                frameEntered.Wait();

                // The frame is held until the timeout has passed, so the shutdown can't finish inside it, however fast
                //  or slow the machine is
                TimeSpan timeout = TimeSpan.FromMilliseconds(kTimeoutMs);
                Assert.IsFalse(await bkg.ShutdownGracefullyAndWaitForCompletion(timeout));
                Assert.IsTrue(bkg.IsActive);
            }
            finally
            {
                releaseFrame.Set();
            }

            // The stop stays requested after a timeout, so the loop stops once the held frame ends
            Assert.IsTrue(await bkg.ShutdownGracefullyAndWaitForCompletion());
            Assert.IsFalse(bkg.IsActive);

            void _HoldFrame (ThreadWorkerDataTracker<int, TNone, int> data)
            {
                frameEntered.Set();
                releaseFrame.Wait();
            }
        }

        [TestMethod]
        public async Task ThreadWorker_ReadingShouldContinue_LeavesTheStopRequested ()
        {
            var frameEntered = new ManualResetEventSlim();
            var stopRequested = new ManualResetEventSlim();
            var secondRead = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var bkg = new ThreadWorker<int, int>();
            bkg.StartThreadLoop(_CheckShouldContinueTwice);

            frameEntered.Wait();
            Task stopped = WhenStopped(bkg);
            bkg.InitiateGracefulShutdown();
            stopRequested.Set();

            // A frame actor checking whether to wrap up early must not take the stop request away from the loop
            Assert.IsFalse(await secondRead.Task);
            await stopped;

            void _CheckShouldContinueTwice (ThreadWorkerDataTracker<int, TNone, int> data)
            {
                frameEntered.Set();
                stopRequested.Wait();
                if (!data.ShouldContinue)
                {
                    secondRead.TrySetResult(data.ShouldContinue);
                }
            }
        }

        [TestMethod]
        public async Task ThreadWorker_Restarted_IgnoresTheEarlierRunsCancellationToken ()
        {
            var firstRunCancellor = new CancellationTokenSource();
            var bkg = new ThreadWorker<int, int>();
            bkg.StartThreadLoop(_DoNothing, firstRunCancellor.Token);
            Task firstRunStopped = WhenStopped(bkg);
            bkg.InitiateGracefulShutdown();
            await firstRunStopped;

            var frameEntered = new ManualResetEventSlim();
            var releaseFrame = new ManualResetEventSlim();
            var shouldContinueAfterCancel = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            bkg.StartThreadLoop(_HoldFrame);

            // Canceled while the second run's frame is held, so the frame reads whatever the cancel did
            frameEntered.Wait();
            firstRunCancellor.Cancel();
            releaseFrame.Set();

            Assert.IsTrue(
                await shouldContinueAfterCancel.Task,
                "Canceling the first run's token stopped the second run"
            );
            Assert.IsTrue(await bkg.ShutdownGracefullyAndWaitForCompletion());

            static void _DoNothing (ThreadWorkerDataTracker<int, TNone, int> data)
            {
            }

            void _HoldFrame (ThreadWorkerDataTracker<int, TNone, int> data)
            {
                frameEntered.Set();
                releaseFrame.Wait();
                shouldContinueAfterCancel.TrySetResult(data.ShouldContinue);
            }
        }

        [TestMethod]
        public async Task ThreadWorker_Dispose_AfterTheLoopStopped_DoesNotThrow ()
        {
            var cancellor = new CancellationTokenSource();
            var bkg = new ThreadWorker<int, int>();
            bkg.StartThreadLoop(_DoNothing, cancellor.Token);
            Task stopped = WhenStopped(bkg);
            cancellor.Cancel();
            await stopped;

            bkg.Dispose();
            Assert.IsFalse(bkg.IsActive);

            static void _DoNothing (ThreadWorkerDataTracker<int, TNone, int> data)
            {
            }
        }

        [TestMethod]
        public async Task ThreadWorker_LoopStopsWithInputQueued_TheWaitForAllInputFinishes ()
        {
            const int kGenerousLimitSeconds = 30;
            var bkg = new ThreadWorker<int, int>();
            bkg.StartThreadLoop(_LeaveInputQueued);
            bkg.InputToProcess.Add(1);
            Task allProcessed = bkg.WhenAllInputProcessingCompleted();

            Task stopped = WhenStopped(bkg);
            bkg.InitiateGracefulShutdown();
            await stopped;

            // Stopping clears the queue, so the wait has nothing left to wait for. The limit only turns a wait that is never
            //  told into a failure instead of a hang; a slow machine is nowhere near it.
            Task finishedFirst = await Task.WhenAny(
                allProcessed,
                Task.Delay(TimeSpan.FromSeconds(kGenerousLimitSeconds))
            );
            Assert.AreSame(
                allProcessed,
                finishedFirst,
                "The wait for all input never finished after the loop stopped"
            );

            static void _LeaveInputQueued (ThreadWorkerDataTracker<int, TNone, int> data)
            {
            }
        }

        [TestMethod]
        public void ThreadWorkerDataQueue_Add_RaisesDataReceivedOncePerAdd ()
        {
            int receivedCount = 0;
            var queue = new ThreadWorkerDataQueue<int> { ManuallyNotifyDataReceivedOnly = false };
            queue.DataReceived += _OnDataReceived;

            queue.Add(1);
            queue.AddRange(new[] { 2, 3 });
            queue.DataReceived -= _OnDataReceived;

            Assert.AreEqual(2, receivedCount);

            void _OnDataReceived (object sender, EventArgs e)
            {
                ++receivedCount;
            }
        }

        [TestMethod]
        public void ThreadWorkerState_RemoveAll_RemovesOnlyTheMatches ()
        {
            var state = new ThreadWorkerState<int>();
            state.AddRange(new[] { 1, 2, 3 });

            int[] removed = state.RemoveAll(item => item == 2);

            CollectionAssert.AreEqual(new[] { 2 }, removed);
            CollectionAssert.AreEqual(new[] { 1, 3 }, state.ToArray());
        }

        /// <summary>
        /// A task that finishes once the worker's loop has stopped, for a test that has to see a stop without going through
        /// the shutdown it is testing
        /// </summary>
        private static Task WhenStopped<TInput, TExecutionState, TOutput> (ThreadWorker<TInput, TExecutionState, TOutput> worker)
        {
            var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            worker.IsActiveChanged += _OnIsActiveChanged;

            // Checked once the handler is in place, so a stop between the check and the subscribe can't be missed
            _CompleteIfStopped();
            return stopped.Task;

            void _OnIsActiveChanged (object sender, EventArgs e)
            {
                _CompleteIfStopped();
            }

            void _CompleteIfStopped ()
            {
                if (!worker.IsActive)
                {
                    worker.IsActiveChanged -= _OnIsActiveChanged;
                    stopped.TrySetResult(true);
                }
            }
        }
    }
}
