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

        protected abstract string GetBindSourceName();

        protected virtual void OnViewModelChanged(object sender, PropertyChangedEventArgs e)
        {
            Debug.Log("View element update : " + this.GetType().Name + " - " + e.PropertyName);
            var propertyFullName = string.Join('.', GetBindSourceName(), e.PropertyName);
            bindings.UpdateBind(propertyFullName);
        }

        public virtual void Bind(IViewController viewModel)
        {
            //logic done in ViewElement<T> due to viewModel type-safety
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
            if (viewController is not T vm)
            {
                if(viewController != default)
                {
                    throw new System.Exception($"Trying to bind view {GetType()} to controller of type {viewController.GetType()}, expected: {typeof(T)}");
                }
                vm = default;
            }

            Unbind();
            this.viewModel = vm;
            RegisterViewModel(vm);
            OnBind();
        }

        private void RegisterViewModel(T viewModel)
        {
            Debug.Log("Registering view model " + GetType().Name + " - " + typeof(T).Name);
            if (viewModel is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged -= OnViewModelChanged;
                npc.PropertyChanged += OnViewModelChanged;
            }

            if(viewModel is INestedObservableProperties nop)
            {
                nop.RegisterNestedProperties();
            }
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
