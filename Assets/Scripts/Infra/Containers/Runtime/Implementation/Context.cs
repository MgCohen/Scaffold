using VContainer.Unity;

namespace Scaffold.Containers
{
    public class Context : IContext
    {
        public Context(LifetimeScope scope)
        {
            this.scope = scope;
            this.parent = null;
        }

        private Context(Context parent)
        {
            this.scope = null;
            this.parent = parent;
        }

        private Context parent;
        private LifetimeScope scope;

        public IContext AddChild<T>() where T : Container, new()
        {
            T container = new T();
            return AddChild(container);
        }

        public IContext AddChild(Container container)
        {
            return Build(container, this);
        }

        public IContext Append<T>() where T : Container, new()
        {
            T container = new T();
            return Append(container);
        }

        public IContext Append(Container container)
        {
            return Build(container, parent);
        }

        public IContext ChangeContext<T>() where T : Container, new()
        {
            T container = new T();
            return ChangeContext(container);
        }

        public IContext ChangeContext(Container container)
        {
            scope.Dispose();
            return Append(container);
        }

        private Context Build(Container container, Context parent)
        {
            Context context = new Context(parent);
            LifetimeScope childScope = container.Build(parent.scope, context);
            context.scope = childScope;
            return context;
        }
    }
}
