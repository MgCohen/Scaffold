namespace Scaffold.Containers
{
    internal class Context : IContext
    {
        internal Context(IContainerScope scope)
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
        private IContainerScope scope;

        internal void SetScope(IContainerScope scope)
        {
            this.scope = scope;
        }

        public IContext AddChild<T>() where T : Container, new()
        {
            return AddChild(new T());
        }

        public IContext AddChild(Container container)
        {
            return Build(container, this);
        }

        public IContext Append<T>() where T : Container, new()
        {
            return Append(new T());
        }

        public IContext Append(Container container)
        {
            return Build(container, parent);
        }

        public IContext ChangeContext<T>() where T : Container, new()
        {
            return ChangeContext(new T());
        }

        public IContext ChangeContext(Container container)
        {
            scope.Dispose();
            return Append(container);
        }

        private Context Build(Container container, Context parent)
        {
            Context childContext = new Context(parent);
            parent.scope.BuildChild(container, childContext, parent.scope.Transform);
            return childContext;
        }
    }
}
