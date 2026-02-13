using UnityEngine;

namespace Scaffold.Navigation
{
    public interface IView
    {
#pragma warning disable IDE1006 // Naming Styles
        public GameObject gameObject { get; }
#pragma warning restore IDE1006 // Naming Styles

        public ViewState State { get; }
        public ViewType Type { get; }
        void Bind(IViewController controller);
        void Open();
        void Close();
        void Focus();
        void Hide();
        void Order(int depth);
    }
}
