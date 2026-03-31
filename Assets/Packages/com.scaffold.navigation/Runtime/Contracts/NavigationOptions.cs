using System;
using UnityEngine;

namespace Scaffold.Navigation.Contracts
{
    [Serializable]
    public class NavigationOptions
    {
        public RenderMode? RenderOverride;
        public bool? CloseAllViews;
    }
}



