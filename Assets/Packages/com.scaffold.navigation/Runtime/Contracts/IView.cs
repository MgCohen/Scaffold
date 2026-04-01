using UnityEngine;

namespace Scaffold.Navigation.Contracts
{
    public interface IView
    {
        public GameObject gameObject { get; }

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




