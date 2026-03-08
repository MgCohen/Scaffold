using Scaffold.MVVM.Binding;
using Scaffold.Navigation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using UnityEngine;

namespace Scaffold.MVVM
{
    public abstract class ViewElement : MonoBehaviour
    {
        protected IBindings bindings = new TreeBinding();

        #region Binding Utility

        protected IBindedProperty<TSource, TTarget> Bind<TSource, TTarget>(Expression<Func<TSource>> source, Expression<Func<TTarget>> target)
        {
            return bindings.RegisterBind(source, target);
        }

        protected IBindedProperty<TSource, TTarget> Bind<TSource, TTarget>(Expression<Func<TSource>> source, Action<TTarget> target)
        {
            return bindings.RegisterBind(source, target);
        }

        protected void BindConverter<TSource, TTarget>(Func<TSource, TTarget> converter)
        {
            GenericConverter<TSource, TTarget> genericConverter = new GenericConverter<TSource, TTarget>(converter);
            BindConverter(genericConverter);
        }

        protected void BindConverter<TSource, TTarget>(Binding.Converter<TSource, TTarget> converter)
        {
            bindings.RegisterConverter(converter);
        }

        protected void BindCollection<TSource, TTarget>(Expression<Func<ICollection<TSource>>> source, ICollectionHandler<TSource, TTarget> handler)
        {
            bindings.RegisterBindCollection(source, handler);
        }


        #endregion

        protected virtual void OnViewModelChanged(object sender, PropertyChangedEventArgs e)
        {
            var elementTypeName = GetType().Name;
            Debug.Log("View element update : " + elementTypeName + " - " + e.PropertyName);
            var bindSourceName = GetBindSourceName();
            var propertyFullName = string.Join('.', bindSourceName, e.PropertyName);
            bindings.UpdateBind(propertyFullName);
        }

        protected abstract string GetBindSourceName();

        public virtual void Bind(IViewController viewModel)
        {
        }

        protected virtual void OnBind()
        {

        }

        protected void Unbind()
        {
            bindings.Unbind();
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
            Unbind();
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
            this.parent = parent;
            Bind(viewModel);
        }

    }
}
