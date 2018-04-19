using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Polly;

namespace atc.utilities
{
    public class ErrorHandlingPolicy
    {
        public static Task<T> ExecuteNetworkCallAsync<T>(Func<Task<T>> operation, Func<Exception, TimeSpan, Task> exceptionHandler)
        {
            return Policy
                .Handle<Exception>(ex => !IsCancellation(ex))
                .WaitAndRetryAsync(
                    sleepDurations: GetRetryTimes(
                        initialWaitTime: TimeSpan.FromSeconds(1.0),
                        maxJitter: TimeSpan.FromSeconds(0.5)),
                    onRetryAsync: exceptionHandler)
                .ExecuteAsync(operation);
        }

        public static Task ExecuteNetworkCallAsync(Func<Task> operation, Func<Exception, TimeSpan, Task> exceptionHandler)
        {
            return Policy
                .Handle<Exception>(ex => !IsCancellation(ex))
                .WaitAndRetryAsync(
                    sleepDurations: GetRetryTimes(
                        initialWaitTime: TimeSpan.FromSeconds(1.0),
                        maxJitter: TimeSpan.FromSeconds(0.5)),
                    onRetryAsync: exceptionHandler)
                .ExecuteAsync(operation);
        }

        public static Task ExecuteRequestAsync(Func<Task> requestHandler)
        {
            var ignoreCancellationExceptions = Policy.Handle<Exception>(ex => IsCancellation(ex)).FallbackAsync(ct => Task.CompletedTask);
            return ignoreCancellationExceptions.ExecuteAsync(requestHandler);
        }

        public static Task<T> ExecuteRequestAsync<T>(Func<Task<T>> requestHandler)
        {
            var ignoreCancellationExceptions = Policy<T>.Handle<Exception>(ex => IsCancellation(ex)).FallbackAsync<T>(ct => Task.FromResult(default(T)));
            return ignoreCancellationExceptions.ExecuteAsync(requestHandler);
        }

        public static bool IsCancellation(Exception ex)
        {
            if (ex is TaskCanceledException || ex is OperationCanceledException)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static TimeSpan GetRandomJitter(TimeSpan maxJitter)
        {
            Random random = new Random();
            double jitterFactor = (random.NextDouble() - 0.5) * 2.0;
            TimeSpan retval = TimeSpan.FromMilliseconds(maxJitter.TotalMilliseconds * jitterFactor);
            return retval;
        }

        // Computes 3 wait periods, subsequent period twice as long as the previous one, with random jitter added (up to +- maxJitter value).
        public static IEnumerable<TimeSpan> GetRetryTimes(TimeSpan initialWaitTime, TimeSpan maxJitter)
        {
            const int NoOfPeriods = 3;
            const double WaitExtensionFactor = 2.0;

            TimeSpan currentBase = initialWaitTime;

            for (int i = 0; i < NoOfPeriods; i++)
            {
                TimeSpan waitPeriod = currentBase + GetRandomJitter(maxJitter);
                yield return waitPeriod;
                currentBase = TimeSpan.FromMilliseconds(currentBase.TotalMilliseconds * WaitExtensionFactor);
            }
        }
    }
}
