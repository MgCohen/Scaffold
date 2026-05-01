namespace Scaffold.Navigation.Contracts
{
    public enum NavigationStackPolicy
    {
        Push = 0,
        ReplaceCurrent = 1,
        ClearBelowCurrentAndPush = 2,
        ClearAllAndPush = 3,
    }
}
