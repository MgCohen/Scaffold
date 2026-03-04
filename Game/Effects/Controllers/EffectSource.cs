using System;
using System.Collections.Generic;

namespace Scaffold.Effects
{
    public class EffectSource
    {
        private Dictionary<Type, Effect> entryPoints = new Dictionary<Type, Effect>();
        private List<IEventSubscription> subscriptions = new List<IEventSubscription>();

        public Effect GetEntryPointEffect<T>() where T: EntryPoint
        {
            if (entryPoints.ContainsKey(typeof(T)))
            {
                return entryPoints[typeof(T)];
            }
            return Effect.NullEffect;
        }
    }
}
