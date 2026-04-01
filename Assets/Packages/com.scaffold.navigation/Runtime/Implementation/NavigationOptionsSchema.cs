using Scaffold.Navigation.Contracts;
using UnityEngine;

namespace Scaffold.Navigation
{
    internal class NavigationOptionsSchema : ViewSchema
    {
        public NavigationOptions Options => options;
        [SerializeField] private NavigationOptions options;
    }
}

