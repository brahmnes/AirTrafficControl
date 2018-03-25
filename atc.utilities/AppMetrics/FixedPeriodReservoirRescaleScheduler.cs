using System;
using System.Collections.Concurrent;
using System.Threading;
using App.Metrics.Scheduling;
using App.Metrics.ReservoirSampling;
using Validation;

namespace atc.utilities.AppMetrics {
    public class FixedPeriodReservoirRescaleScheduler : IReservoirRescaleScheduler {
        private readonly ConcurrentBag<IRescalingReservoir> reservoirs_;
        private Timer rescalingTimer_;
        private bool isDisposed_;
        private TimeSpan rescalePeriod_;

        public FixedPeriodReservoirRescaleScheduler(TimeSpan rescalePeriod) {
            Requires.That(rescalePeriod >= TimeSpan.FromSeconds(1),
                          nameof(rescalePeriod),
                          "Rescale period of {0:c} is too small (must be at least 1 second)",
                          rescalePeriod);

            rescalePeriod_ = rescalePeriod;
            isDisposed_ = false;
            reservoirs_ = new ConcurrentBag<IRescalingReservoir>();
            rescalingTimer_ = new Timer(DoRescaling, null, rescalePeriod, Timeout.InfiniteTimeSpan);
        }

        public void Dispose() {
            isDisposed_ = true;
            rescalingTimer_.Dispose();
        }

        public void RemoveSchedule(IRescalingReservoir reservoir) {
            Requires.NotNull(reservoir, nameof(reservoir));

            reservoirs_.TryTake(out IRescalingReservoir unused);
        }

        public void ScheduleReScaling(IRescalingReservoir reservoir) {
            Requires.NotNull(reservoir, nameof(reservoir));
            Verify.NotDisposed(!isDisposed_, $"{nameof(FixedPeriodReservoirRescaleScheduler)} was disposed");

            reservoirs_.Add(reservoir);
        }

        private void DoRescaling(object state) {
            // It is safe to iterate over ConcurrentBag, even when it is being concurrently modified
            foreach (var reservoir in reservoirs_) {
                reservoir.Rescale();
            }

            rescalingTimer_.Change(rescalePeriod_, Timeout.InfiniteTimeSpan);
        }
    }
}
