using UnityEngine;
using Scaffold.Navigation;

namespace Scaffold.MVVM
{
    public class View<T> : ViewElement<T>, IView where T : IViewModel
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

        protected virtual void ToggleView(bool state)
        {
            gameObject.SetActive(state);
        }

        void Navigation.IView.Close()
        {
            ToggleView(false);
            OnClose();
            state = ViewState.Closed;
        }

        void Navigation.IView.Focus()
        {
            ToggleView(true);
            OnFocus();
        }

        void Navigation.IView.Open()
        {
            ToggleView(true);
            state = ViewState.Open;
            OnOpen();
        }

        void Navigation.IView.Hide()
        {
            ToggleView(false);
            OnHide();
        }

        void Navigation.IView.Order(int viewOrder)
        {
            Order(viewOrder);
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

