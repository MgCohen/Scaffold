using VContainer.Unity;

namespace Scaffold.Containers
{
    public class Context : IContext
    {
        public Context(LifetimeScope scope, ContainerConfig config)
        {
            this.scope = scope;
            this.config = config;
            this.parent = null;
        }

        private Context(Context parent, ContainerConfig config)
        {
            this.scope = null;
            this.config = config;
            this.parent = parent;
        }

        private Context parent;
        private ContainerConfig config;
        private LifetimeScope scope;

        public IContext AddChild<T>() where T : Container, new()
        {
            T container = new T();
            return AddChild(container);
        }

        public IContext AddChild(Container container)
        {
            return Build(container, this, config);
        }

        public IContext Append<T>() where T : Container, new()
        {
            T container = new T();
            return Append(container);
        }

        public IContext Append(Container container)
        {
            return Build(container, parent, config);
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

        private Context Build(Container container, Context parent, ContainerConfig config)
        {
            Context context = new Context(parent, config);
            LifetimeScope childScope = container.Build(parent.scope, config, context);
            context.scope = childScope;
            return context;
        }
    }
}
