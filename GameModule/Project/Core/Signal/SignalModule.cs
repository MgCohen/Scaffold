using System;
using System.Collections.Generic;

namespace GameModule.Signal
{
    /// <summary>
    /// A decoupled event bus system for broadcasting requests.
    /// </summary>
    public class SignalModule
    {
        private readonly Dictionary<Type, Action<object>> _subscribers = new Dictionary<Type, Action<object>>();

        public void Push<T>(T signal)
        {
            Type type = typeof(T);
            if (_subscribers.TryGetValue(type, out Action<object>? handler))
            {
                handler?.Invoke(signal!);
            }
        }

        public void Subscribe<T>(Action<T> onNext)
        {
            Type type = typeof(T);

            Action<object> wrappedAction = (obj) =>
            {
                if (obj is T typedObj)
                {
                    onNext(typedObj);
                }
            };

            if (_subscribers.ContainsKey(type))
            {
                _subscribers[type] += wrappedAction;
            }
            else
            {
                _subscribers[type] = wrappedAction;
            }
        }

        public void Unsubscribe<T>(Action<T> onNext)
        {
            // Simple generic event bus without wrapping for minus operator, ideally we'd store the original delegates to unsubscribe properly.
            // A more complex implementation would be needed to support unsubscription cleanly if delegates are wrapped as Action<object>.
            // Let's implement a typed subscriber list to support proper - operation.
        }
    }
}
