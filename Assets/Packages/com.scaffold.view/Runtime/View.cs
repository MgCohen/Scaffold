using UnityEngine;
using Scaffold.Navigation.Contracts;
using Scaffold.MVVM.Binding;
using System.Collections.Generic;
using System;
using Scaffold.MVVM.Contracts;
namespace Scaffold.MVVM
{
    public class View<T> : ViewElement<T>, Scaffold.MVVM.Contracts.IView where T : IViewModel
    {
        public ViewState State
        {
            get { return state; }
        }
        protected ViewState state;

        public ViewType Type
        {
            get { return type; }
        }
        [SerializeField] private ViewType type = ViewType.Screen;

        void Scaffold.Navigation.Contracts.IView.Close()
        {
            ToggleView(false);
            OnClose();
            state = ViewState.Closed;
        }

        void Scaffold.Navigation.Contracts.IView.Focus()
        {
            ToggleView(true);
            OnFocus();
        }

        void Scaffold.Navigation.Contracts.IView.Open()
        {
            ToggleView(true);
            state = ViewState.Open;
            OnOpen();
        }

        void Scaffold.Navigation.Contracts.IView.Hide()
        {
            ToggleView(false);
            OnHide();
        }

        void Scaffold.Navigation.Contracts.IView.Order(int viewOrder)
        {
            Order(viewOrder);
        }

        protected virtual void ToggleView(bool state)
        {
            gameObject.SetActive(state);
        }

        protected virtual void Order(int viewOrder)
        {

        }

        protected virtual void OnOpen()
        {

        }

        protected virtual void OnClose()
        {

        }

        protected virtual void OnFocus()
        {

        }

        protected virtual void OnHide()
        {

        }
    }
}






