#pragma warning disable SCA3002 // Unity Bee batch compile can omit newly added standalone scripts until the Editor refreshes the script graph; both enums must live in this file for CI validate-changes to compile Scaffold.Navigation.
namespace Scaffold.Navigation.Contracts
{
    public enum ViewAssetSource
    {
        Addressables = 0,
        DirectPrefab = 1
    }

    public enum ViewFilter
    {
        Any = 0,
        SpecificViews = 1
    }
}
#pragma warning restore SCA3002
