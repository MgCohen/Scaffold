namespace Scaffold.Navigation.Contracts
{
    public interface IViewController
    {
        void Bind(INavigation navigation);
        void Close();
    }
}




