using System;
using System.Collections.Generic;

namespace LiveOps.Signal
{

    public class SignalModule
    {
        private readonly object _gate = new();
        private readonly Dictionary<Type, List<Subscriber>> _byType = new();

        private readonly struct Subscriber
        {
            public readonly Delegate Original;
            public readonly Action<object> Wrapped;

            public Subscriber(Delegate original, Action<object> wrapped)
            {
                Original = original;
                Wrapped = wrapped;
            }
        }

        public void Push<T>(T signal)
        {
            List<Action<object>>? toRun = null;
            Type type = typeof(T);
            lock (_gate)
            {
                if (!_byType.TryGetValue(type, out List<Subscriber>? list) || list is null)
                {
                    return;
                }

                toRun = new List<Action<object>>(list.Count);
                for (int i = 0; i < list.Count; i++)
                {
                    toRun.Add(list[i].Wrapped);
                }
            }

            if (toRun is null)
            {
                return;
            }

            for (int i = 0; i < toRun.Count; i++)
            {
                toRun[i](signal!);
            }
        }

        public void Subscribe<T>(Action<T> onNext)
        {
            if (onNext is null)
            {
                return;
            }

            Type type = typeof(T);
            lock (_gate)
            {
                if (!_byType.TryGetValue(type, out List<Subscriber>? list))
                {
                    list = new List<Subscriber>();
                    _byType[type] = list;
                }

                Action<object> wrapped = obj =>
                {
                    if (obj is T t)
                    {
                        onNext(t);
                    }
                };
                list.Add(new Subscriber(onNext, wrapped));
            }
        }

        public void Unsubscribe<T>(Action<T> onNext)
        {
            if (onNext is null)
            {
                return;
            }

            Type type = typeof(T);
            lock (_gate)
            {
                if (!_byType.TryGetValue(type, out List<Subscriber>? list))
                {
                    return;
                }

                list.RemoveAll(s => s.Original.Equals(onNext));
            }
        }
    }
}
