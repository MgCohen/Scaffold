using System;
using System.Collections.Generic;
using System.ComponentModel;
using Scaffold.MVVM.Binding;
using Scaffold.MVVM.Contracts;
using Scaffold.Navigation.Contracts;
using UnityEngine;

namespace Scaffold.MVVM
{
    public abstract class ViewElement<T> : ViewElement where T : IViewModel
    {
        [SerializeField]
        protected T viewModel;

        public sealed override void Bind(IViewController viewController)
        {
            T vm = viewController switch
            {
                T typed => typed,
                null => default,
                _ => throw new Exception($"Trying to bind view {GetType()} to controller of type {viewController.GetType()}, expected: {typeof(T)}"),
            };
            if (!EqualityComparer<T>.Default.Equals(viewModel, default))
            {
                Unbind();
            }
            this.viewModel = vm;
            Debug.Log("Registering view model " + GetType().Name + " - " + typeof(T).Name);
            WireViewModelSubscriptions(vm);
            OnBind();
        }

        private void WireViewModelSubscriptions(T vm)
        {
            if (vm is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged -= OnViewModelChanged;
                npc.PropertyChanged += OnViewModelChanged;
            }
            if (vm is INestedObservableProperties nop)
            {
                nop.RegisterNestedProperties();
            }
        }

        protected sealed override string GetBindSourceName()
        {
            return nameof(viewModel);
        }
    }
}
