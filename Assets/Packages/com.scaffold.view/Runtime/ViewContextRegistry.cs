using System;
using System.Collections.Generic;
using Scaffold.Navigation.Contracts;

namespace Scaffold.MVVM
{
    public sealed class ViewContextRegistry : IViewContext
    {
        private readonly Dictionary<Type, object> services = new Dictionary<Type, object>();

        public void Register<T>(T instance) where T : class
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            services[typeof(T)] = instance;
        }

        public bool TryResolve<T>(out T service) where T : class
        {
            if (services.TryGetValue(typeof(T), out object obj) && obj is T typed)
            {
                service = typed;
                return true;
            }

            service = null;
            return false;
        }
    }
}
