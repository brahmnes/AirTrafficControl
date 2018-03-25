using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using App.Metrics;
using App.Metrics.Concurrency;
using App.Metrics.ReservoirSampling;
using App.Metrics.ReservoirSampling.ExponentialDecay;
using App.Metrics.Scheduling;
using Validation;

namespace atc.utilities.AppMetrics {
    public class ForwardDecayingLowWeightThresholdReservoir : IRescalingReservoir, IDisposable {
        private static readonly string ReservoirDisposedMessage = $"{nameof(ForwardDecayingLowWeightThresholdReservoir)} was disposed";
        private static readonly string LockNotTaken = "Operation failed because the reservoir could not ensure exclusive access to internal data structures";

        private readonly double alpha_;
        private readonly double sampleWeightThreshold_;
        private readonly IClock clock_;
        private readonly IReservoirRescaleScheduler rescaleScheduler_;
        private readonly int sampleSize_;
        private SortedList<double, WeightedSample> values_;

        private long count_ = 0L;
        private bool disposed_ = false;
        private SpinLock lock_ = new SpinLock(enableThreadOwnerTracking: false);
        private long startTime_;
        private double sum_ = 0.0;


        public ForwardDecayingLowWeightThresholdReservoir(
            int sampleSize,
            double alpha,
            double sampleWeightThreshold,
            IClock clock,
            IReservoirRescaleScheduler rescaleScheduler) {
            sampleSize_ = sampleSize;
            alpha_ = alpha;
            sampleWeightThreshold_ = sampleWeightThreshold;
            clock_ = clock;
            rescaleScheduler_ = rescaleScheduler;

            values_ = new SortedList<double, WeightedSample>(sampleSize, ReverseOrderDoubleComparer.Instance);
            startTime_ = clock.Seconds;
            rescaleScheduler_.ScheduleReScaling(this);
        }

        public void Dispose() {
            disposed_ = true;
            rescaleScheduler_.RemoveSchedule(this);
        }

        public IReservoirSnapshot GetSnapshot(bool resetReservoir) {
            Verify.NotDisposed(!disposed_, ReservoirDisposedMessage);

            WeightedSnapshot snapshot = null;

            ExecuteAsCriticalSection(() => {
                snapshot = new WeightedSnapshot(count_, sum_, values_.Values);
                if (resetReservoir) {
                    ResetReservoir();
                }
            });

            return snapshot;
        }

        public IReservoirSnapshot GetSnapshot() => GetSnapshot(false);

        public void Rescale() {
            Verify.NotDisposed(!disposed_, ReservoirDisposedMessage);

            ExecuteAsCriticalSection(() => {
                var oldStartTime = startTime_;
                startTime_ = clock_.Seconds;

                var scalingFactor = Math.Exp(-alpha_ * (startTime_ - oldStartTime));

                var newSamples = new Dictionary<double, WeightedSample>(values_.Count);

                foreach (var keyValuePair in values_) {
                    var sample = keyValuePair.Value;

                    var newWeight = sample.Weight * scalingFactor;
                    if (newWeight < sampleWeightThreshold_) {
                        continue;
                    }

                    var newKey = keyValuePair.Key * scalingFactor;
                    var newSample = new WeightedSample(sample.Value, sample.UserValue, sample.Weight * scalingFactor);
                    newSamples.Add(newKey, newSample);
                }

                values_ = new SortedList<double, WeightedSample>(newSamples, ReverseOrderDoubleComparer.Instance);

                // Need to reset the samples counter after rescaling
                count_ = values_.Count;
                sum_ = values_.Values.Aggregate(0L, (current, sample) => current + sample.Value);
            });
        }

        public void Reset() {
            ExecuteAsCriticalSection(() => ResetReservoir());
        }

        public void Update(long value, string userValue) {
            Update(value, userValue, clock_.Seconds);
        }

        public void Update(long value) {
            Update(value, null, clock_.Seconds);
        }

        private void Update(long value, string userValue, long timestamp) {
            Verify.NotDisposed(!disposed_, ReservoirDisposedMessage);

            var itemWeight = Math.Exp(alpha_ * (timestamp - startTime_));
            var sample = new WeightedSample(value, userValue, itemWeight);

            var random = 0.0;

            // Prevent division by 0
            // TODO: what about underflow?
            while (random.Equals(0.0)) {
                random = ThreadLocalRandom.NextDouble();
            }

            var priority = itemWeight / random;

            ExecuteAsCriticalSection(() => {
                count_++;
                sum_ += value;

                if (count_ <= sampleSize_) {
                    values_[priority] = sample;
                }
                else {
                    var first = values_.Keys[values_.Count - 1];
                    if (first < priority) {
                        values_.Remove(first);
                        values_[priority] = sample;
                    }
                }
            });
        }

        private void ResetReservoir() {
            values_.Clear();
            count_ = 0L;
            sum_ = 0.0;
            startTime_ = clock_.Seconds;
        }

        private void ExecuteAsCriticalSection(Action action) {
            var lockTaken = false;
            try {
                lock_.Enter(ref lockTaken);
                if (!lockTaken) {
                    throw new InvalidOperationException(LockNotTaken);
                }

                action();
            }
            finally {
                if (lockTaken) {
                    lock_.Exit();
                }
            }
        }

        private sealed class ReverseOrderDoubleComparer : IComparer<double> {
            public static readonly IComparer<double> Instance = new ReverseOrderDoubleComparer();

            public int Compare(double x, double y) { return y.CompareTo(x); }
        }
    }
}
