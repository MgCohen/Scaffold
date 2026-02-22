using Scaffold.Navigation;
using System;
using UnityEngine;

namespace Scaffold.Navigation
{
    public class ServerNavigationController : MonoBehaviour, INavigation
    {
        public NavigationPoint CurrentPoint
        {
            get { return default; }
        }

        public void Close<TViewController>(TViewController controller) where TViewController : IViewController
        {
        }

        public IViewController GetPreviousView()
        {
            return default;
        }

        public void Initialize()
        {
        }

        public IViewController Open(Type viewType, bool closeCurrent = false, NavigationOptions options = null)
        {
            return default;
        }

        public void Open<TViewController>(TViewController controller, bool closeCurrent = false, NavigationOptions options = null) where TViewController : IViewController
        {
        }

        public IViewController Return()
        {
            return default;
        }

        public TViewController TryGetOpenView<TViewController>()
        {
            return default;
        }
    }
}
