namespace Scaffold.MVVM.Binding
{
    public interface IBindContext
    {
        bool IsEmpty { get; }

        void Update();

        void Unbind();
    }
}



