#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>
    /// Sample-only event bus. Sequential await on Publish; subscribers run in registration order.
    /// <para>Production hosts pick their own bus + ordering policy (parallel, priority, etc.) — this
    /// stand-in exists purely to validate the entry-catalog wiring loop in tests. NOT part of the
    /// package: the framework knows nothing about how triggers are dispatched.</para>
    /// </summary>
    public sealed class EventBus
    {
        readonly Dictionary<Type, List<Func<object, Task>>> _subs = new();

        public void Subscribe<T>(Func<T, Task> handler) where T : class
        {
            if (!_subs.TryGetValue(typeof(T), out var list))
                _subs[typeof(T)] = list = new List<Func<object, Task>>();
            list.Add(o => handler((T)o));
        }

        public async Task Publish<T>(T evt) where T : class
        {
            if (!_subs.TryGetValue(typeof(T), out var list)) return;
            foreach (var h in list)
                await h(evt).ConfigureAwait(false);
        }
    }
}
