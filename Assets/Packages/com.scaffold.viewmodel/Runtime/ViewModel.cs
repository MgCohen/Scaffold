using System.Linq.Expressions;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using Scaffold.MVVM.Binding;
using UnityEngine;
using Scaffold.Navigation.Contracts;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;
using System;
using Scaffold.MVVM.Contracts;
namespace Scaffold.MVVM
{
    [NestedObservableObject]
    [BindSource(typeof(TreeBinding))]
    public abstract partial class ViewModel : ObservableObject, IViewModel
    {
        protected INavigation navigation;

        public void Bind(INavigation navigation)
        {
            if (navigation is null)
            {
                throw new ArgumentNullException(nameof(navigation));
            }
            ClearBindings();
            this.navigation = navigation;
            if (this is INestedObservableProperties nestedObservableProperties)
            {
                nestedObservableProperties.RegisterNestedProperties();
            }
            Initialize();
        }

        protected virtual void Initialize()
        {

        }

        protected T BindChildViewModel<T>(T viewModel) where T : IViewModel
        {
            viewModel.Bind(navigation);
            return viewModel;
        }

        protected sealed override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            UpdateBinding(e.PropertyName);
            base.OnPropertyChanged(e);
        }

        public void Close()
        {
            if (navigation == null)
            {
                return;
            }
            navigation.Close(this);
            OnClosed();
        }

        protected virtual void OnClosed()
        {

        }
    }
}






