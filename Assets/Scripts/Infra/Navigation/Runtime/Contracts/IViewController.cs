namespace Scaffold.Navigation
{
    public interface IViewController
    {
        void Bind(INavigation navigation);
        void Close();
    }
}
