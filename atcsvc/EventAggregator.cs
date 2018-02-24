using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Validation;

namespace atcsvc
{
    public class EventAggregator<T> : IEventAggregator<T>
    {
        private readonly Subject<T> subject = new Subject<T>();

        public void Dispose()
        {
            subject.Dispose();
        }

        public void Publish(T ev)
        {
            Requires.NotNullAllowStructs(ev, nameof(ev));

            subject.OnNext(ev);
        }

        public IObservable<T> Subscribe()
        {
            return subject.AsObservable();
        }
    }
}
