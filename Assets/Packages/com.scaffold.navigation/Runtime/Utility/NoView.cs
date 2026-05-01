using UnityEngine;
using Scaffold.Types;
using Scaffold.Events.Contracts;
using Scaffold.Events;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using Scaffold.Navigation.Contracts;
namespace Scaffold.Navigation.Utility
{
    internal class NoView : IView
    {
        public GameObject gameObject => throw new System.NotImplementedException();

        public ViewState State => throw new System.NotImplementedException();

        public ViewType Type => throw new System.NotImplementedException();

        public void Bind(IViewController controller)
        {
            GuardCall();
            throw new System.NotImplementedException();
        }

        public void Close()
        {
            GuardCall();
            throw new System.NotImplementedException();
        }

        public void Focus()
        {
            GuardCall();
            throw new System.NotImplementedException();
        }

        public void Open()
        {
            GuardCall();
            throw new System.NotImplementedException();
        }

        public void Hide()
        {
            GuardCall();
            throw new System.NotImplementedException();
        }

        public void Order(int depth)
        {
            GuardCall();
            throw new System.NotImplementedException();
        }

        private void GuardCall()
        {
            if (this == null)
            {
                throw new System.InvalidOperationException("NoView instance is not available.");
            }
        }
    }
}







