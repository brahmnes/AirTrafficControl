using System;

namespace atcsvc
{
    public interface IEventAggregator<T>: IDisposable
    {
        IObservable<T> Subscribe();
        void Publish(T ev);
    }
}
