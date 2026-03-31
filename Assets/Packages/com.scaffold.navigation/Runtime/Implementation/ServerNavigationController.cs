using UnityEngine;
using Scaffold.Types;
using Scaffold.Events.Contracts;
using Scaffold.Events;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using Scaffold.Navigation.Contracts;
namespace Scaffold.Navigation
{
    public class ServerNavigationController : MonoBehaviour, INavigation
    {
        public IViewController CurrentController
        {
            get { return default; }
        }

        public void Close<TViewController>(TViewController controller) where TViewController : IViewController
        {
            GuardServerState();
        }

        public IViewController GetPreviousView()
        {
            GuardServerState();
            return default;
        }

        public void Initialize()
        {
            GuardServerState();
        }

        public IViewController Open(Type viewType, bool closeCurrent = false, NavigationOptions options = null)
        {
            GuardServerState();
            return default;
        }

        public void Open<TViewController>(TViewController controller, bool closeCurrent = false, NavigationOptions options = null) where TViewController : IViewController
        {
            GuardServerState();
        }

        public IViewController Return()
        {
            GuardServerState();
            return default;
        }

        public TViewController TryGetOpenView<TViewController>()
        {
            GuardServerState();
            return default;
        }

        private void GuardServerState()
        {
            if (this == null)
            {
                throw new InvalidOperationException("Server navigation controller is not available.");
            }
        }
    }
}





