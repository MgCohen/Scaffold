using Scaffold.Navigation.Contracts;
using Scaffold.MVVM.Binding;
using Scaffold.MVVM.Contracts;
using System.Collections.Generic;
using Scaffold.Types;
using System;
using UnityEngine;

namespace Scaffold.MVVM.BaseEvents
{
    [Serializable]
    public class NavigateViewEvent : ViewEvent
    {
        public NavigationType Navigation => navigation;
        [SerializeField] private NavigationType navigation;

        public TypeReference View => view;
        [TypeReferenceFilter(typeof(Scaffold.MVVM.Contracts.IView))]
        [SerializeField] private TypeReference view;

        public bool CloseCurrent => closeCurrent;
        [SerializeField] private bool closeCurrent = false;

        public enum NavigationType
        {
            Open = 0,
            Return = 10,
        }
    }
}




