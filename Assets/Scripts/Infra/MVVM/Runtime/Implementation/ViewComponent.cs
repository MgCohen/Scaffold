using Scaffold.MVVM.Binding;
using System.ComponentModel;
using UnityEngine;

namespace Scaffold.MVVM
{
    public class ViewComponent<T> : ViewElement<T> where T : IViewModel
    {
    }
}
