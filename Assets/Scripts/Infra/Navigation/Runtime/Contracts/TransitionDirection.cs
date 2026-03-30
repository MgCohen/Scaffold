namespace Scaffold.Navigation.Contracts
{
    [System.Flags]
    public enum TransitionDirection
    {
        FromThisView = 1 << 1,
        ToThisView = 1 << 2,
    }
}
