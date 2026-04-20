using UnityEngine;
using Scaffold.Navigation.Contracts;
using Scaffold.MVVM.Binding;
using System;
using Scaffold.MVVM.Contracts;
namespace Scaffold.MVVM
{
    public class View<T> : ViewElement<T>, Scaffold.MVVM.Contracts.IView, IViewContextHost where T : IViewModel
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

        public IViewContext Context => viewContext ??= new ViewContextRegistry();

        private ViewContextRegistry viewContext;

        protected virtual bool AutoBindChildViewComponents => false;

        private bool isHidden;

        void Scaffold.Navigation.Contracts.IView.Close()
        {
            ToggleView(false);
            OnClose(hiding: false);
            state = ViewState.Closed;
            isHidden = false;
        }

        void Scaffold.Navigation.Contracts.IView.Focus()
        {
            bool wasHidden = isHidden;
            ToggleView(true);
            OnOpen(wasHidden: wasHidden);
            isHidden = false;
        }

        void Scaffold.Navigation.Contracts.IView.Open()
        {
            ToggleView(true);
            state = ViewState.Open;
            OnOpen(wasHidden: false);
            isHidden = false;
        }

        void Scaffold.Navigation.Contracts.IView.Hide()
        {
            ToggleView(false);
            OnClose(hiding: true);
            isHidden = true;
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

        protected virtual void OnOpen(bool wasHidden)
        {

        }

        protected virtual void OnClose(bool hiding)
        {

        }

        protected override void OnBind()
        {
            base.OnBind();
            if (AutoBindChildViewComponents)
            {
                BindMatchingChildViewElements(viewModel);
            }
        }

        private void BindMatchingChildViewElements(T vm)
        {
            if (vm == null)
            {
                return;
            }

            foreach (ViewElement child in GetComponentsInChildren<ViewElement>(true))
            {
                TryAutoBindChild(vm, child);
            }
        }

        private void TryAutoBindChild(T vm, ViewElement child)
        {
            if (ReferenceEquals(child, this) || child is Scaffold.Navigation.Contracts.IView)
            {
                return;
            }

            if (!TryParseViewElementModelType(child, out Type modelType) || modelType != typeof(T))
            {
                return;
            }

            child.Bind(vm);
        }

        private static bool TryParseViewElementModelType(ViewElement element, out Type modelType)
        {
            modelType = null;
            for (Type t = element.GetType(); t != null && t != typeof(object); t = t.BaseType)
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ViewElement<>))
                {
                    modelType = t.GetGenericArguments()[0];
                    return true;
                }
            }

            return false;
        }
    }
}
