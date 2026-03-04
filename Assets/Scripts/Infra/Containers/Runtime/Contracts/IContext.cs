namespace Scaffold.Containers
{
    public interface IContext
    {
        IContext AddChild(Container container);
        IContext AddChild<T>() where T : Container, new();
        IContext Append(Container container);
        IContext Append<T>() where T : Container, new();
        IContext ChangeContext(Container container);
        IContext ChangeContext<T>() where T : Container, new();
    }
}
