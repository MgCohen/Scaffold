using System;
using UnityEngine;

namespace Scaffold.Navigation
{
    [Serializable]
    public class NavigationOptions
    {
        public RenderMode? RenderOverride;
        public bool? CloseAllViews;
    }
}
