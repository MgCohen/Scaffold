using Scaffold.Maps;
using System;
using System.Linq.Expressions;

namespace Scaffold.MVVM.Binding
{
    internal class BindRegistry
    {
        public BindRegistry(BindGroups groups)
        {
            this.groups = groups;
        }

        private BindGroups groups;
        private Map<string, Type, IBindContext> registeredContexts = new Map<string, Type, IBindContext>();

        public BindContext<TSource> GetContext<TSource>(Expression<Func<TSource>> source)
        {
            string path = GetBindKey(source);
            Type type = typeof(TSource);
            return GetContext(path, type, source);
        }

        private string GetBindKey<TSource>(Expression<Func<TSource>> source)
        {
            return source.GetPropertyName();
        }

        private BindContext<TSource> GetContext<TSource>(string path, Type type, Expression<Func<TSource>> source)
        {
            if (registeredContexts.TryGetValue(path, type, out IBindContext context))
            {
                return context as BindContext<TSource>;
            }
            return CreateContext(path, type, source);
        }

        private BindContext<TSource> CreateContext<TSource>(string path, Type type, Expression<Func<TSource>> source)
        {
            Func<TSource> setter = source.Compile();
            BindContext<TSource> context = new BindContext<TSource>(setter);
            RegisterContext(path, type, context);
            return context;
        }

        private void RegisterContext(string path, Type type, IBindContext context)
        {
            registeredContexts.Add(path, type, context);
            groups.Register(path, context);
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
