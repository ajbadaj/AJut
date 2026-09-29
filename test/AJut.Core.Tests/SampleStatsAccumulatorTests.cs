namespace AJut.Core.UnitTests
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using AJut;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SampleStatsAccumulatorTests
    {
        // The widest a bucket gets, relative to the values in it
        private const double kMaxBucketRatio = 1.125;

        // ---- Basics ----

        [TestMethod]
        public void Empty_SnapshotIsAllZero ()
        {
            var stats = new SampleStatsAccumulator();
            SampleStatsAccumulator.Snapshot snapshot = stats.TakeSnapshot();

            Assert.AreEqual(0L, snapshot.Count);
            Assert.AreEqual(0.0, snapshot.Sum);
            Assert.AreEqual(0.0, snapshot.Mean);
            Assert.AreEqual(0.0, snapshot.Min);
            Assert.AreEqual(0.0, snapshot.Max);
            Assert.AreEqual(default(DateTime), snapshot.MaxAtUtc);
            Assert.AreEqual(0.0, snapshot.P50);
            Assert.AreEqual(0.0, snapshot.P99);
            Assert.AreEqual("n=0", snapshot.Describe("ms"));
        }

        [TestMethod]
        public void DefaultSnapshot_IsSafeToRead ()
        {
            SampleStatsAccumulator.Snapshot snapshot = default;
            Assert.AreEqual(0.0, snapshot.GetPercentile(50.0));
            Assert.AreEqual("n=0", snapshot.Describe());
        }

        [TestMethod]
        public void Record_TracksCountSumMinMaxMean ()
        {
            var stats = new SampleStatsAccumulator();
            stats.Record(4.0);
            stats.Record(1.0);
            stats.Record(7.0);

            SampleStatsAccumulator.Snapshot snapshot = stats.TakeSnapshot();
            Assert.AreEqual(3L, snapshot.Count);
            Assert.AreEqual(12.0, snapshot.Sum);
            Assert.AreEqual(4.0, snapshot.Mean);
            Assert.AreEqual(1.0, snapshot.Min);
            Assert.AreEqual(7.0, snapshot.Max);
        }

        [TestMethod]
        public void Record_ReturnsTrueOnlyForANewMax ()
        {
            var stats = new SampleStatsAccumulator();
            Assert.IsTrue(stats.Record(5.0), "The first sample after a reset is always the max");
            Assert.IsFalse(stats.Record(3.0));
            Assert.IsFalse(stats.Record(5.0), "Equal to the max is not a new max");
            Assert.IsTrue(stats.Record(7.0));
        }

        [TestMethod]
        public void Record_NaN_IsIgnored ()
        {
            var stats = new SampleStatsAccumulator();
            stats.Record(2.0);
            Assert.IsFalse(stats.Record(Double.NaN));

            SampleStatsAccumulator.Snapshot snapshot = stats.TakeSnapshot();
            Assert.AreEqual(1L, snapshot.Count);
            Assert.AreEqual(2.0, snapshot.Sum);
        }

        [TestMethod]
        public void MaxAtUtc_IsWhenTheMaxWasRecorded ()
        {
            var stats = new SampleStatsAccumulator();
            DateTime before = DateTime.UtcNow;
            stats.Record(10.0);
            DateTime after = DateTime.UtcNow;

            Thread.Sleep(20);
            stats.Record(1.0);
            stats.Record(10.0);

            DateTime maxAt = stats.TakeSnapshot().MaxAtUtc;
            Assert.AreEqual(DateTimeKind.Utc, maxAt.Kind);
            Assert.IsTrue(maxAt >= before && maxAt <= after, "A smaller or equal sample later must not move it");
        }

        // ---- Threshold ----

        [TestMethod]
        public void Threshold_CountsSamplesStrictlyOver ()
        {
            var stats = new SampleStatsAccumulator(threshold: 5.0);
            stats.Record(4.0);
            stats.Record(5.0);
            stats.Record(5.1);
            stats.Record(100.0);

            SampleStatsAccumulator.Snapshot snapshot = stats.TakeSnapshot();
            Assert.AreEqual(5.0, stats.Threshold);
            Assert.AreEqual(5.0, snapshot.Threshold);
            Assert.AreEqual(2L, snapshot.OverThresholdCount);
        }

        [TestMethod]
        public void Threshold_None_CountsNothing ()
        {
            var stats = new SampleStatsAccumulator();
            stats.Record(Double.PositiveInfinity);

            SampleStatsAccumulator.Snapshot snapshot = stats.TakeSnapshot();
            Assert.IsNull(snapshot.Threshold);
            Assert.AreEqual(0L, snapshot.OverThresholdCount);
        }

        [TestMethod]
        public void Threshold_NaN_Throws ()
        {
            Assert.ThrowsException<ArgumentException>(() => new SampleStatsAccumulator(threshold: Double.NaN));
        }

        // ---- Percentiles ----

        [TestMethod]
        public void Percentile_SmallWholeNumbers_AreExact ()
        {
            var stats = new SampleStatsAccumulator();
            for (int value = 1; value <= 10; ++value)
            {
                stats.Record(value);
            }

            SampleStatsAccumulator.Snapshot snapshot = stats.TakeSnapshot();
            Assert.AreEqual(5.0, snapshot.P50);
            Assert.AreEqual(10.0, snapshot.P95);
            Assert.AreEqual(3.0, snapshot.GetPercentile(30.0));
            Assert.AreEqual(1.0, snapshot.GetPercentile(0.0));
            Assert.AreEqual(10.0, snapshot.GetPercentile(100.0));
        }

        [TestMethod]
        public void Percentile_IdenticalSamples_ReportThatValue ()
        {
            var stats = new SampleStatsAccumulator();
            for (int index = 0; index < 1000; ++index)
            {
                stats.Record(0.4375);
            }

            SampleStatsAccumulator.Snapshot snapshot = stats.TakeSnapshot();
            Assert.AreEqual(0.4375, snapshot.P50);
            Assert.AreEqual(0.4375, snapshot.P99);
        }

        [TestMethod]
        public void Percentile_IsNeverBelowTheTruth_AndAtMostOneBucketAbove ()
        {
            var random = new Random(1234);
            var stats = new SampleStatsAccumulator();
            double[] samples = new double[20000];
            for (int index = 0; index < samples.Length; ++index)
            {
                // Spread across about seven doublings either side of 1, the way timings in ms tend to be
                samples[index] = Math.Pow(2.0, (random.NextDouble() * 14.0) - 7.0);
                stats.Record(samples[index]);
            }

            Array.Sort(samples);
            SampleStatsAccumulator.Snapshot snapshot = stats.TakeSnapshot();
            foreach (double percentile in new[] { 1.0, 10.0, 25.0, 50.0, 75.0, 90.0, 95.0, 99.0, 99.9 })
            {
                long rank = (long)Math.Ceiling(percentile * samples.Length / 100.0);
                double truth = samples[rank - 1];
                double reported = snapshot.GetPercentile(percentile);

                Assert.IsTrue(reported >= truth, $"p{percentile} reported {reported}, below the true {truth}");
                Assert.IsTrue(reported < truth * kMaxBucketRatio, $"p{percentile} reported {reported}, more than a bucket over the true {truth}");
                Assert.IsTrue(Array.BinarySearch(samples, reported) >= 0, $"p{percentile} reported {reported}, which was never recorded");
            }
        }

        [TestMethod]
        public void Percentile_OutOfHistogramRange_StillBoundedByWhatWasRecorded ()
        {
            var stats = new SampleStatsAccumulator();
            stats.Record(-5.0);
            stats.Record(0.0);
            stats.Record(1e-12);
            stats.Record(1e12);
            stats.Record(5e12);

            SampleStatsAccumulator.Snapshot snapshot = stats.TakeSnapshot();
            Assert.AreEqual(-5.0, snapshot.Min);
            Assert.AreEqual(5e12, snapshot.Max);
            Assert.AreEqual(-5.0, snapshot.GetPercentile(0.0));
            Assert.AreEqual(1e-12, snapshot.GetPercentile(40.0), "The three underflow samples share a bucket, reported as its largest");
            Assert.AreEqual(5e12, snapshot.GetPercentile(80.0), "The two overflow samples share a bucket, reported as its largest");
            Assert.AreEqual(5e12, snapshot.GetPercentile(100.0));
        }

        [TestMethod]
        public void Percentile_OutOfRange_Throws ()
        {
            var stats = new SampleStatsAccumulator();
            stats.Record(1.0);
            SampleStatsAccumulator.Snapshot snapshot = stats.TakeSnapshot();

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => snapshot.GetPercentile(-1.0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => snapshot.GetPercentile(100.1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => snapshot.GetPercentile(Double.NaN));
        }

        // ---- Resetting ----

        [TestMethod]
        public void TakeSnapshot_KeepsCollecting ()
        {
            var stats = new SampleStatsAccumulator();
            stats.Record(1.0);
            stats.TakeSnapshot();
            stats.Record(2.0);

            Assert.AreEqual(2L, stats.TakeSnapshot().Count);
        }

        [TestMethod]
        public void TakeSnapshotAndReset_CapturesThenStartsOver ()
        {
            var stats = new SampleStatsAccumulator(threshold: 1.5);
            stats.Record(1.0);
            stats.Record(2.0);

            SampleStatsAccumulator.Snapshot first = stats.TakeSnapshotAndReset();
            stats.Record(8.0);
            SampleStatsAccumulator.Snapshot second = stats.TakeSnapshot();

            Assert.AreEqual(2L, first.Count);
            Assert.AreEqual(2.0, first.Max);
            Assert.AreEqual(1L, first.OverThresholdCount);

            Assert.AreEqual(1L, second.Count);
            Assert.AreEqual(8.0, second.Min);
            Assert.AreEqual(8.0, second.P50);
            Assert.AreEqual(1L, second.OverThresholdCount);
            Assert.IsTrue(second.StartedAtUtc >= first.StartedAtUtc);
        }

        [TestMethod]
        public void Snapshots_DoNotChange_WhenMoreIsRecorded ()
        {
            var stats = new SampleStatsAccumulator();
            stats.Record(1.0);
            stats.Record(2.0);

            SampleStatsAccumulator.Snapshot copied = stats.TakeSnapshot();
            stats.Record(1000.0);
            stats.Record(1000.0);
            stats.Record(1000.0);
            Assert.AreEqual(2.0, copied.P95);

            SampleStatsAccumulator.Snapshot handedOff = stats.TakeSnapshotAndReset();
            stats.Record(1.0);
            stats.Record(1.0);
            stats.Record(1.0);
            stats.Record(1.0);
            stats.Record(1.0);
            Assert.AreEqual(1000.0, handedOff.P95);
            Assert.AreEqual(1000.0, handedOff.P50);
        }

        [TestMethod]
        public void Reset_ThrowsAwayWhatWasRecorded ()
        {
            var stats = new SampleStatsAccumulator();
            stats.Record(5.0);
            stats.Reset();

            Assert.AreEqual(0L, stats.TakeSnapshot().Count);
            Assert.IsTrue(stats.Record(1.0), "The first sample after a reset is a new max, whatever came before");
        }

        [TestMethod]
        public void Duration_CoversTheTimeSinceTheLastReset ()
        {
            var stats = new SampleStatsAccumulator();
            Thread.Sleep(30);

            SampleStatsAccumulator.Snapshot snapshot = stats.TakeSnapshotAndReset();
            Assert.IsTrue(snapshot.Duration >= TimeSpan.FromMilliseconds(25), $"Duration was {snapshot.Duration}");
            Assert.IsTrue(stats.TakeSnapshot().Duration < snapshot.Duration, "The reset starts the next duration from zero");
        }

        // ---- Describe ----

        [TestMethod]
        public void Describe_UsesFixedKeysInAFixedOrder ()
        {
            var stats = new SampleStatsAccumulator(threshold: 2.5);
            stats.Record(1.0);
            stats.Record(2.0);
            stats.Record(3.0);
            stats.Record(4.0);

            SampleStatsAccumulator.Snapshot snapshot = stats.TakeSnapshot();
            string expectedAt = snapshot.MaxAtUtc.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
            Assert.AreEqual($"n=4 min=1ms mean=2.5ms p50=2ms p95=4ms p99=4ms max=4ms at={expectedAt}Z over=2", snapshot.Describe("ms"));
        }

        [TestMethod]
        public void Describe_WithoutThresholdOrUnit_LeavesThemOut ()
        {
            var stats = new SampleStatsAccumulator();
            stats.Record(3.0);

            string description = stats.TakeSnapshot().Describe();
            Assert.IsTrue(description.StartsWith("n=1 min=3 mean=3 p50=3 p95=3 p99=3 max=3 at="), description);
            Assert.IsFalse(description.Contains("over="), description);
        }

        [TestMethod]
        public void Describe_FormatsLargeAndSmallValuesReadably ()
        {
            var stats = new SampleStatsAccumulator();
            stats.Record(0.000123);
            stats.Record(5242880.0);

            string description = stats.TakeSnapshot().Describe("B");
            Assert.IsTrue(description.Contains(" min=0.000123B "), description);
            Assert.IsTrue(description.Contains(" max=5242880B "), description);
        }

        [TestMethod]
        public void Describe_IgnoresTheCurrentCulture ()
        {
            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                var stats = new SampleStatsAccumulator();
                stats.Record(1.25);
                Assert.IsTrue(stats.TakeSnapshot().Describe().Contains(" min=1.25 "));
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        // ---- Hot path ----

        [TestMethod]
        public void Record_DoesNotAllocate ()
        {
            var stats = new SampleStatsAccumulator(threshold: 1.0);
            stats.Record(0.5);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 10000; ++index)
            {
                stats.Record(index * 0.01);
            }

            Assert.AreEqual(before, GC.GetAllocatedBytesForCurrentThread());
        }

        [TestMethod]
        public void Record_FromManyThreads_WhileReporting_LosesNothing ()
        {
            const int kThreadCount = 4;
            const int kSamplesPerThread = 100000;

            var stats = new SampleStatsAccumulator();
            long reportedCount = 0;
            double reportedSum = 0.0;
            int finishedThreads = 0;

            var recorders = Enumerable.Range(0, kThreadCount).Select(_ => new Thread(RecordSamples)).ToList();
            recorders.ForEach(thread => thread.Start());
            while (Volatile.Read(ref finishedThreads) < kThreadCount)
            {
                SampleStatsAccumulator.Snapshot snapshot = stats.TakeSnapshotAndReset();
                reportedCount += snapshot.Count;
                reportedSum += snapshot.Sum;
            }

            recorders.ForEach(thread => thread.Join());
            SampleStatsAccumulator.Snapshot last = stats.TakeSnapshotAndReset();
            reportedCount += last.Count;
            reportedSum += last.Sum;

            Assert.AreEqual((long)kThreadCount * kSamplesPerThread, reportedCount);
            Assert.AreEqual((double)kThreadCount * kSamplesPerThread, reportedSum);

            void RecordSamples ()
            {
                for (int index = 0; index < kSamplesPerThread; ++index)
                {
                    stats.Record(1.0);
                }

                Interlocked.Increment(ref finishedThreads);
            }
        }
    }
}
