using Scaffold.MVVM.Binding;
using Scaffold.Navigation;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace Scaffold.MVVM
{
    [BindSource(typeof(TreeBinding))]
    public abstract partial class ViewElement : MonoBehaviour
    {
        protected virtual void OnViewModelChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e == null) { return; }
            var elementTypeName = GetType().Name;
            Debug.Log("View element update : " + elementTypeName + " - " + e.PropertyName);
            var bindSourceName = GetBindSourceName();
            var propertyFullName = string.Join('.', bindSourceName, e.PropertyName);
            UpdateBinding(propertyFullName);
        }

        protected abstract string GetBindSourceName();

        public virtual void Bind(IViewController viewModel)
        {
            if (viewModel == null) { return; }
        }

        protected virtual void OnBind()
        {

        }

        protected void Unbind()
        {
            ClearBindings();
            OnUnbind();
        }

        protected virtual void OnUnbind()
        {

        }
    }

    public abstract class ViewElement<T> : ViewElement where T : IViewModel
    {
        [SerializeField]
        protected T viewModel;

        public sealed override void Bind(IViewController viewController)
        {
            var vm = GetViewModelOrDefault(viewController);
            if (!EqualityComparer<T>.Default.Equals(viewModel, default))
            {
                Unbind();
            }
            this.viewModel = vm;
            RegisterViewModel(vm);
            OnBind();
        }

        private T GetViewModelOrDefault(IViewController viewController)
        {
            if (viewController is T vm) { return vm; }
            if (viewController == default) { return default; }
            var viewType = GetType();
            var controllerType = viewController.GetType();
            throw new System.Exception($"Trying to bind view {viewType} to controller of type {controllerType}, expected: {typeof(T)}");
        }

        private void RegisterViewModel(T viewModel)
        {
            var typeName = GetType().Name;
            Debug.Log("Registering view model " + typeName + " - " + typeof(T).Name);
            SubscribePropertyChanged(viewModel);
            RegisterNestedProperties(viewModel);
        }

        private void SubscribePropertyChanged(T viewModel)
        {
            if (viewModel is not INotifyPropertyChanged npc) { return; }
            npc.PropertyChanged -= OnViewModelChanged;
            npc.PropertyChanged += OnViewModelChanged;
        }

        private void RegisterNestedProperties(T viewModel)
        {
            if (viewModel is INestedObservableProperties nop) { nop.RegisterNestedProperties(); }
        }

        protected sealed override string GetBindSourceName()
        {
            return nameof(viewModel);
        }
    }

    public abstract class ViewElement<T, J> : ViewElement<J> where T: ViewElement where J : IViewModel
    {
        protected T parent;

        public void Bind(T parent, J viewModel)
        {
            if (parent == null) { throw new System.ArgumentNullException(nameof(parent)); }
            if (viewModel == null) { return; }
            this.parent = parent;
            Bind(viewModel);
        }

    }
}
