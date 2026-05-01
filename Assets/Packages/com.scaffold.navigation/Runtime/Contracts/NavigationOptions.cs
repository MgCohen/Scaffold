using System;
using UnityEngine;

namespace Scaffold.Navigation.Contracts
{
    [Serializable]
    public class NavigationOptions
    {
        public NavigationStackPolicy StackPolicy
        {
            get { return stackPolicy; }
            set { stackPolicy = value; }
        }

        [SerializeField]
        private NavigationStackPolicy stackPolicy = NavigationStackPolicy.Push;

        public RenderMode? RenderOverride;

        public bool? CloseAllViews;
    }
}
