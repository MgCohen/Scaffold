using UnityEngine;

namespace Scaffold.Navigation
{
    public class NoView : IView
    {
        public GameObject gameObject => throw new System.NotImplementedException();

        public ViewState State => throw new System.NotImplementedException();

        public ViewType Type => throw new System.NotImplementedException();

        public void Bind(IViewController controller)
        {
            throw new System.NotImplementedException();
        }

        public void Close()
        {
            throw new System.NotImplementedException();
        }

        public void Focus()
        {
            throw new System.NotImplementedException();
        }

        public void Open()
        {
            throw new System.NotImplementedException();
        }

        public void Hide()
        {
            throw new System.NotImplementedException();
        }

        public void Order(int depth)
        {
            throw new System.NotImplementedException();
        }
    }
}
