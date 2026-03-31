using System;
using System.ComponentModel;
using System.Collections.Generic;
using Scaffold.MVVM.Binding;
using Scaffold.MVVM.Contracts;
using Scaffold.Navigation.Contracts;
using UnityEngine;

namespace Scaffold.MVVM
{
    [BindSource(typeof(TreeBinding))]
    public abstract partial class ViewElement : MonoBehaviour
    {
        protected void OnViewModelChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e == null)
            {
                return;
            }
            var elementTypeName = GetType().Name;
            Debug.Log("View element update : " + elementTypeName + " - " + e.PropertyName);
            var bindSourceName = GetBindSourceName();
            var propertyFullName = string.Join('.', bindSourceName, e.PropertyName);
            UpdateBinding(propertyFullName);
        }

        protected abstract string GetBindSourceName();

        public virtual void Bind(IViewController viewModel)
        {
            if (viewModel == null)
            {
                return;
            }
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
}
