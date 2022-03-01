namespace AJut.Core.UnitTests
{
    using AJut.Threading;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Threading;

    [TestClass]
    public class ThreadingTests
    {
        [TestMethod]
        public void ThreadWorker_ProofOfConcept ()
        {
            var finalOutput = new List<string>();
            var bkg = new ThreadWorker<int, string>();

            bkg.StartThreadLoop(_DoBkgWork);
            bkg.OutputResults.DataReceived += _OnOutputDataReceived;

            bkg.InputToProcess.Add(5);
            bkg.InputToProcess.Add(1088050);

            bkg.InputToProcess.Add(1010155001);

            bkg.ShutdownGracefullyAndWaitForCompletion().ContinueWith(_=>
            {
                Assert.IsFalse(bkg.IsActive);
                string result = String.Join(",", finalOutput);
                Assert.AreEqual("5,1,88,5,1,1,155,1", result);
            });

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
        public void ThreadWorker_CancellationToken ()
        {
            var finalOutput = new List<string>();
            var bkg = new ThreadWorker<int, string>();

            CancellationTokenSource cancellor = new CancellationTokenSource();
            bkg.StartThreadLoop(_DoBkgWork, cancellor.Token);
            bkg.OutputResults.DataReceived += _OnOutputDataReceived;

            bkg.InputToProcess.Add(5);
            bkg.InputToProcess.Add(1088050);
            bkg.WhenAllInputProcessingCompleted().ContinueWith(_ =>
            {
                cancellor.Cancel();
                bkg.InputToProcess.Add(1010155001);
                bkg.ShutdownGracefullyAndWaitForCompletion().ContinueWith(_1 =>
                {
                    Assert.IsFalse(bkg.IsActive);
                    string result = String.Join(",", finalOutput);
                    Assert.AreEqual("5,1,88,5", result);
                });
            });

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
        public void ThreadWorker_ExecutionState_Test ()
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
            bkg.ShutdownGracefullyAndWaitForCompletion().ContinueWith(_ =>
            {
                Assert.IsFalse(bkg.IsActive);
                string result = String.Join(",", finalOutput);
                Assert.AreEqual("1,88", result);
            });

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
    }
}
