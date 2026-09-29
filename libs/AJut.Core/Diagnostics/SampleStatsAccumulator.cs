namespace AJut
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// Collects numeric samples (durations, sizes, counts, anything) from any number of threads into a count, sum, min, max
    /// (and when the max happened), a count over an optional threshold, and a histogram for percentiles. Take a
    /// <see cref="Snapshot"/> whenever you want to report, and reset to start over.
    /// </summary>
    /// <remarks>
    /// <see cref="Record"/> is built for hot threads: it never allocates, and holds a lock only long enough to update a handful
    /// of fields. The histogram has 8 buckets per doubling from 2^-32 to 2^32 (about 2.3e-10 to 4.3e9), so any unit that keeps
    /// its values in that range works the same - milliseconds, seconds, bytes. Values outside it, zero and negatives included,
    /// still count toward everything; they just share an underflow or an overflow bucket, which makes percentiles that land
    /// there coarse. Each instance costs about 8 KB.
    /// </remarks>
    public sealed class SampleStatsAccumulator
    {
        // A double is 2^exponent * 1.mantissa, so the exponent picks the doubling and the top mantissa bits pick an even slice
        //  of it. That buckets without a log call, and puts every bucket edge on an exact value.
        private const int kDoubleMantissaBits = 52;
        private const int kDoubleExponentMask = 0x7FF;
        private const int kDoubleExponentBias = 1023;
        private const int kSubBucketBits = 3;
        private const int kSubBucketMask = (1 << kSubBucketBits) - 1;
        private const int kLowestExponent = -32;
        private const int kHighestExponent = 31;
        private const double kLowestBucketedValue = 1.0 / 4294967296.0;
        private const double kOverflowValue = 4294967296.0;
        private const int kUnderflowBucket = 0;
        private const int kFirstInRangeBucket = kUnderflowBucket + 1;
        private const int kOverflowBucket = kFirstInRangeBucket + ((kHighestExponent - kLowestExponent + 1) << kSubBucketBits);
        private const int kBucketCount = kOverflowBucket + 1;

        private readonly object m_lock = new object();
        private readonly double m_overThreshold;
        private long[] m_bucketCounts;
        private double[] m_bucketMaxes;
        private long m_count;
        private double m_sum;
        private double m_min;
        private double m_max;
        private DateTime m_maxAtUtc;
        private long m_overThresholdCount;
        private DateTime m_startedAtUtc;
        private long m_startedAtTimestamp;

        // ===========[ Setup/Construction/Teardown ]==========================

        /// <summary>
        /// Builds a new accumulator, collecting from now.
        /// </summary>
        /// <param name="threshold">If given, the snapshot counts how many samples were strictly greater than this</param>
        public SampleStatsAccumulator (double? threshold = null)
        {
            if (threshold.HasValue && Double.IsNaN(threshold.Value))
            {
                throw new ArgumentException("A threshold has to be a number", nameof(threshold));
            }

            this.Threshold = threshold;
            m_overThreshold = threshold ?? Double.PositiveInfinity;
            m_bucketCounts = new long[kBucketCount];
            m_bucketMaxes = new double[kBucketCount];
            this.StartOver(DateTime.UtcNow, Stopwatch.GetTimestamp());
        }

        // ===========[ Properties ]===========================================

        /// <summary>
        /// Samples strictly greater than this are counted in <see cref="Snapshot.OverThresholdCount"/>. Null when no threshold
        /// was given.
        /// </summary>
        public double? Threshold { get; }

        // ===========[ Public Interface Methods ]=============================

        /// <summary>
        /// Adds a sample. Safe from any thread, never allocates, and only reads the clock when the sample is a new max.
        /// </summary>
        /// <returns>True when this sample is the largest since the last reset (the first sample after a reset always is), so a
        /// caller can react to a new worst case without keeping its own state</returns>
        /// <remarks>
        /// NaN is not a sample, so it is ignored and returns false.
        /// </remarks>
        public bool Record (double value)
        {
            if (Double.IsNaN(value))
            {
                return false;
            }

            int bucket = BucketIndexFor(value);
            lock (m_lock)
            {
                ++m_count;
                m_sum += value;
                if (value > m_overThreshold)
                {
                    ++m_overThresholdCount;
                }

                if (m_bucketCounts[bucket] == 0 || value > m_bucketMaxes[bucket])
                {
                    m_bucketMaxes[bucket] = value;
                }

                ++m_bucketCounts[bucket];

                if (value < m_min)
                {
                    m_min = value;
                }

                if (m_count == 1 || value > m_max)
                {
                    m_max = value;
                    m_maxAtUtc = DateTime.UtcNow;
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Captures everything recorded since the last reset, and keeps collecting.
        /// </summary>
        public Snapshot TakeSnapshot ()
        {
            // Made before taking the lock, so all the lock covers is the copy
            var bucketCounts = new long[kBucketCount];
            var bucketMaxes = new double[kBucketCount];
            long nowTimestamp = Stopwatch.GetTimestamp();
            lock (m_lock)
            {
                Array.Copy(m_bucketCounts, bucketCounts, kBucketCount);
                Array.Copy(m_bucketMaxes, bucketMaxes, kBucketCount);
                return this.BuildSnapshot(bucketCounts, bucketMaxes, nowTimestamp);
            }
        }

        /// <summary>
        /// Captures everything recorded since the last reset and starts over, as one step - no sample recorded from another
        /// thread can land between the two and go missing.
        /// </summary>
        public Snapshot TakeSnapshotAndReset ()
        {
            // The snapshot takes the current histogram as is, so the fresh one is made before taking the lock, and nothing is
            //  copied or cleared while holding it
            var freshBucketCounts = new long[kBucketCount];
            var freshBucketMaxes = new double[kBucketCount];
            DateTime nowUtc = DateTime.UtcNow;
            long nowTimestamp = Stopwatch.GetTimestamp();
            lock (m_lock)
            {
                Snapshot snapshot = this.BuildSnapshot(m_bucketCounts, m_bucketMaxes, nowTimestamp);
                m_bucketCounts = freshBucketCounts;
                m_bucketMaxes = freshBucketMaxes;
                this.StartOver(nowUtc, nowTimestamp);
                return snapshot;
            }
        }

        /// <summary>
        /// Throws away everything recorded so far and starts over.
        /// </summary>
        public void Reset ()
        {
            var freshBucketCounts = new long[kBucketCount];
            var freshBucketMaxes = new double[kBucketCount];
            DateTime nowUtc = DateTime.UtcNow;
            long nowTimestamp = Stopwatch.GetTimestamp();
            lock (m_lock)
            {
                m_bucketCounts = freshBucketCounts;
                m_bucketMaxes = freshBucketMaxes;
                this.StartOver(nowUtc, nowTimestamp);
            }
        }

        // ===========[ Helpers ]==============================================

        private static int BucketIndexFor (double value)
        {
            if (value < kLowestBucketedValue)
            {
                return kUnderflowBucket;
            }

            if (value >= kOverflowValue)
            {
                return kOverflowBucket;
            }

            long bits = BitConverter.DoubleToInt64Bits(value);
            int exponent = (int)((bits >> kDoubleMantissaBits) & kDoubleExponentMask) - kDoubleExponentBias;
            int subBucket = (int)((bits >> (kDoubleMantissaBits - kSubBucketBits)) & kSubBucketMask);
            return kFirstInRangeBucket + ((exponent - kLowestExponent) << kSubBucketBits) + subBucket;
        }

        /// <summary>
        /// Only call with <see cref="m_lock"/> held.
        /// </summary>
        private Snapshot BuildSnapshot (long[] bucketCounts, double[] bucketMaxes, long nowTimestamp)
        {
            // Min and max sit at the infinities while nothing is recorded, which a snapshot shouldn't pass on
            bool isEmpty = m_count == 0;
            return new Snapshot(
                count: m_count,
                sum: m_sum,
                min: isEmpty ? 0.0 : m_min,
                max: isEmpty ? 0.0 : m_max,
                maxAtUtc: m_maxAtUtc,
                threshold: this.Threshold,
                overThresholdCount: m_overThresholdCount,
                startedAtUtc: m_startedAtUtc,
                duration: Stopwatch.GetElapsedTime(m_startedAtTimestamp, nowTimestamp),
                bucketCounts: bucketCounts,
                bucketMaxes: bucketMaxes
            );
        }

        /// <summary>
        /// Only call with <see cref="m_lock"/> held, or from the constructor. Leaves the histogram alone.
        /// </summary>
        private void StartOver (DateTime nowUtc, long nowTimestamp)
        {
            m_count = 0;
            m_sum = 0.0;
            m_min = Double.PositiveInfinity;
            m_max = Double.NegativeInfinity;
            m_maxAtUtc = default;
            m_overThresholdCount = 0;
            m_startedAtUtc = nowUtc;
            m_startedAtTimestamp = nowTimestamp;
        }

        // ===========[ Subclasses ]===========================================

        /// <summary>
        /// What a <see cref="SampleStatsAccumulator"/> held at the moment it was captured: everything recorded since its last
        /// reset. Never changes once taken, whatever is recorded afterward. With nothing recorded, every value is zero.
        /// </summary>
        public readonly struct Snapshot
        {
            private const double kWholeNumberFloor = 100.0;
            private const string kSignificantDigitsFormat = "G3";
            private const string kWholeNumberFormat = "0";
            private const string kMaxAtFormat = "HH:mm:ss.fff";
            private const int kDescribeCapacity = 128;

            private readonly long[] m_bucketCounts;
            private readonly double[] m_bucketMaxes;

            internal Snapshot (
                long count, double sum, double min, double max, DateTime maxAtUtc,
                double? threshold, long overThresholdCount,
                DateTime startedAtUtc, TimeSpan duration,
                long[] bucketCounts, double[] bucketMaxes
            )
            {
                this.Count = count;
                this.Sum = sum;
                this.Min = min;
                this.Max = max;
                this.MaxAtUtc = maxAtUtc;
                this.Threshold = threshold;
                this.OverThresholdCount = overThresholdCount;
                this.StartedAtUtc = startedAtUtc;
                this.Duration = duration;
                m_bucketCounts = bucketCounts;
                m_bucketMaxes = bucketMaxes;
            }

            /// <summary>
            /// How many samples were recorded since the last reset.
            /// </summary>
            public long Count { get; }

            public double Sum { get; }

            public double Mean => this.Count == 0 ? 0.0 : this.Sum / this.Count;

            public double Min { get; }

            public double Max { get; }

            /// <summary>
            /// When <see cref="Max"/> was recorded, in UTC - the first time, if it was recorded more than once.
            /// </summary>
            public DateTime MaxAtUtc { get; }

            /// <summary>
            /// The threshold the <see cref="SampleStatsAccumulator"/> was built with, or null if it had none.
            /// </summary>
            public double? Threshold { get; }

            /// <summary>
            /// How many samples were strictly greater than <see cref="Threshold"/> (always zero with no threshold).
            /// </summary>
            public long OverThresholdCount { get; }

            /// <summary>
            /// When collecting started, in UTC - construction or the last reset.
            /// </summary>
            public DateTime StartedAtUtc { get; }

            /// <summary>
            /// How long it had been collecting when this was taken.
            /// </summary>
            public TimeSpan Duration { get; }

            /// <summary>
            /// The median - see <see cref="GetPercentile"/> for how close it is.
            /// </summary>
            public double P50 => this.GetPercentile(50.0);

            /// <summary>
            /// The 95th percentile - see <see cref="GetPercentile"/> for how close it is.
            /// </summary>
            public double P95 => this.GetPercentile(95.0);

            /// <summary>
            /// The 99th percentile - see <see cref="GetPercentile"/> for how close it is.
            /// </summary>
            public double P99 => this.GetPercentile(99.0);

            /// <summary>
            /// The value that at least <paramref name="percentile"/> percent of the samples were at or below.
            /// </summary>
            /// <param name="percentile">From 0 to 100. Zero gives <see cref="Min"/> and 100 gives <see cref="Max"/></param>
            /// <remarks>
            /// This comes from the histogram, and is the largest sample recorded in the bucket the percentile lands in - so it
            /// is always a value that was actually recorded, and never below the true percentile. Between 2^-32 and 2^32 it is
            /// at most one bucket (12.5%) above it, and exact whenever that bucket only saw one distinct value, which covers
            /// small whole numbers. Outside that range it can be as far off as the underflow or overflow bucket is wide.
            /// </remarks>
            public double GetPercentile (double percentile)
            {
                if (Double.IsNaN(percentile) || percentile < 0.0 || percentile > 100.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(percentile), percentile, "A percentile has to be from 0 to 100");
                }

                if (this.Count == 0 || m_bucketCounts == null)
                {
                    return 0.0;
                }

                if (percentile == 0.0)
                {
                    return this.Min;
                }

                // Nearest rank - the smallest sample with at least that share of the samples at or below it. Multiplied before
                //  dividing so a whole percentile of a whole count doesn't pick up rounding and step to the next rank.
                long rank = Math.Clamp((long)Math.Ceiling(percentile * this.Count / 100.0), 1L, this.Count);
                long samplesSeen = 0;
                for (int bucket = 0; bucket < m_bucketCounts.Length; ++bucket)
                {
                    samplesSeen += m_bucketCounts[bucket];
                    if (samplesSeen >= rank)
                    {
                        return m_bucketMaxes[bucket];
                    }
                }

                return this.Max;
            }

            /// <summary>
            /// A one line summary, for example <c>n=12000 min=0.1ms mean=0.45ms p50=0.42ms p95=1.3ms p99=2.1ms max=7.31ms at=21:05:05.225Z over=3</c>.
            /// The keys and their order are fixed, so searching logs for them keeps working.
            /// </summary>
            /// <param name="unit">Appended to every value that is a sample (min, mean, the percentiles, max), or nothing if null</param>
            /// <remarks>
            /// <c>at</c> is when the max was recorded, as a UTC time of day. <c>over</c> is only there when there is a
            /// threshold. Numbers use the invariant culture, with three significant digits below 100 and whole numbers from
            /// there up. With nothing recorded it is just <c>n=0</c>.
            /// </remarks>
            public string Describe (string unit = null)
            {
                if (this.Count == 0)
                {
                    return "n=0";
                }

                unit ??= String.Empty;
                var text = new StringBuilder(kDescribeCapacity);
                text.Append("n=").Append(this.Count.ToString(CultureInfo.InvariantCulture));
                AppendSampleValue(text, " min=", this.Min, unit);
                AppendSampleValue(text, " mean=", this.Mean, unit);
                AppendSampleValue(text, " p50=", this.P50, unit);
                AppendSampleValue(text, " p95=", this.P95, unit);
                AppendSampleValue(text, " p99=", this.P99, unit);
                AppendSampleValue(text, " max=", this.Max, unit);
                text.Append(" at=").Append(this.MaxAtUtc.ToString(kMaxAtFormat, CultureInfo.InvariantCulture)).Append('Z');
                if (this.Threshold.HasValue)
                {
                    text.Append(" over=").Append(this.OverThresholdCount.ToString(CultureInfo.InvariantCulture));
                }

                return text.ToString();
            }

            public override string ToString () => this.Describe();

            private static void AppendSampleValue (StringBuilder text, string key, double value, string unit)
            {
                text.Append(key).Append(FormatValue(value)).Append(unit);
            }

            private static string FormatValue (double value)
            {
                // G3 on its own would switch to exponents from 1000 up, which reads badly for sizes and long durations
                return Math.Abs(value) >= kWholeNumberFloor
                    ? value.ToString(kWholeNumberFormat, CultureInfo.InvariantCulture)
                    : value.ToString(kSignificantDigitsFormat, CultureInfo.InvariantCulture);
            }
        }
    }
}
