using System;
using System.Linq.Expressions;
using Scaffold.Maps;
namespace Scaffold.MVVM.Binding
{
    internal class BindRegistry
    {
        public BindRegistry(BindGroups groups, IBindingDeferredCoordinator coordinator)
        {
            if (groups is null)
            {
                throw new ArgumentNullException(nameof(groups));
            }
            if (coordinator is null)
            {
                throw new ArgumentNullException(nameof(coordinator));
            }
            this.groups = groups;
            this.coordinator = coordinator;
        }

        private readonly BindGroups groups;
        private readonly IBindingDeferredCoordinator coordinator;
        private readonly Map<string, Type, IBindContext> registeredContexts = new Map<string, Type, IBindContext>();

        public RegistrationContext<TSource> GetOrCreateContext<TSource>(Expression<Func<TSource>> source)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            string path = source.GetPropertyName();
            Type type = typeof(TSource);
            if (registeredContexts.TryGetValue(path, type, out IBindContext found) && found is BindContext<TSource> existing)
            {
                return new RegistrationContext<TSource>(path, type, existing);
            }
            Func<TSource> getter = source.Compile();
            BindContext<TSource> context = new BindContext<TSource>(getter, coordinator);
            registeredContexts.Add(path, type, context);
            groups.Register(path, context);
            return new RegistrationContext<TSource>(path, type, context);
        }

        public void RemoveIfEmpty(string path, Type type, IBindContext context)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));
            if (type is null) throw new ArgumentNullException(nameof(type));
            if (context is null) throw new ArgumentNullException(nameof(context));
            if (!CanRemove(path, type, context)) return;
            groups.Unregister(path, context);
            registeredContexts.Remove(path, type);
        }

        private bool CanRemove(string path, Type type, IBindContext context)
        {
            if (!context.IsEmpty) return false;
            if (!registeredContexts.TryGetValue(path, type, out IBindContext registered)) return false;
            return ReferenceEquals(registered, context);
        }

        internal void Clear()
        {
            foreach (IBindContext context in registeredContexts.Values)
            {
                context.Unbind();
            }

            registeredContexts.Clear();
        }
    }
}
