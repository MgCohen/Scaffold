using Scaffold.Maps;
using System;
using System.Linq.Expressions;

namespace Scaffold.MVVM.Binding
{
    internal class RegistrationContext<TSource>
    {
        public RegistrationContext(string path, Type sourceType, BindContext<TSource> context)
        {
            Path = path;
            SourceType = sourceType;
            Context = context;
        }

        public string Path { get; }
        public Type SourceType { get; }
        public BindContext<TSource> Context { get; }
    }

    internal class BindRegistry
    {
        public BindRegistry(BindGroups groups)
        {
            if (groups is null) { throw new ArgumentNullException(nameof(groups)); }
            this.groups = groups;
        }

        private readonly BindGroups groups;
        private readonly Map<string, Type, IBindContext> registeredContexts = new Map<string, Type, IBindContext>();

        public RegistrationContext<TSource> GetOrCreateContext<TSource>(Expression<Func<TSource>> source)
        {
            if (source is null) { throw new ArgumentNullException(nameof(source)); }
            string path = source.GetPropertyName();
            Type type = typeof(TSource);
            BindContext<TSource> context = GetContext(path, type, source);
            return new RegistrationContext<TSource>(path, type, context);
        }

        private BindContext<TSource> GetContext<TSource>(string path, Type type, Expression<Func<TSource>> source)
        {
            if (registeredContexts.TryGetValue(path, type, out IBindContext context))
            {
                return context as BindContext<TSource>;
            }

            Func<TSource> getter = source.Compile();
            BindContext<TSource> createdContext = new BindContext<TSource>(getter);
            registeredContexts.Add(path, type, createdContext);
            groups.Register(path, createdContext);
            return createdContext;
        }

        public void RemoveIfEmpty(string path, Type type, IBindContext context)
        {
            if (path is null) { throw new ArgumentNullException(nameof(path)); }
            if (type is null) { throw new ArgumentNullException(nameof(type)); }
            if (context is null) { throw new ArgumentNullException(nameof(context)); }
            if (context.IsEmpty == false) { return; }

            if (registeredContexts.TryGetValue(path, type, out IBindContext registered) == false) { return; }
            if (ReferenceEquals(registered, context) == false) { return; }

            groups.Unregister(path, context);
            registeredContexts.Remove(path, type);
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
