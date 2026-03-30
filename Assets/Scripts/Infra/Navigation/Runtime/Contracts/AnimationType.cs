namespace Scaffold.Navigation.Contracts
{
    [System.Flags]
    public enum AnimationType
    {
        Opening = 1 << 1,
        Closing = 1 << 2,
        Hiding = 1 << 3,
        Focusing = 1 << 4
    }
}
