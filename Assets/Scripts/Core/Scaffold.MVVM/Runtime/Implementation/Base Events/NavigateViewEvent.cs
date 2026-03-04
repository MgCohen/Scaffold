using Scaffold.Types;
using System;
using UnityEngine;

namespace Scaffold.MVVM
{
    [Serializable]
    public class NavigateViewEvent : ViewEvent
    {
        public NavigationType Navigation => navigation;
        [SerializeField] private NavigationType navigation;

        public Type View => view.Type;
        [TypeReferenceFilter(typeof(IView))]
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
