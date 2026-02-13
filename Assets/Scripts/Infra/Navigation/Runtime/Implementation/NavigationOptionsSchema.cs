using Scaffold.MVVM;
using UnityEngine;

#if ADDRESSABLES
using UnityEngine.AddressableAssets;
#endif

namespace Scaffold.Navigation
{
    public class NavigationOptionsSchema : ViewSchema
    {
        public NavigationOptions Options => options;
        [SerializeField] private NavigationOptions options;
    }
}
