using UnityEngine;
using Scaffold.Navigation.Contracts;
using Scaffold.MVVM.Binding;
using System.Collections.Generic;
using System;
using Scaffold.MVVM.Contracts;
namespace Scaffold.MVVM
{
    public class ViewComponent<T> : ViewElement<T> where T : IViewModel
    {
    }
}




