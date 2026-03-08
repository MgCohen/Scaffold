using NUnit.Framework;
using UnityEngine;

namespace Scaffold.Navigation.Tests
{
    public class NavigationTests
    {
        [Test]
        public void NavigationOptions_DefaultState_AllFieldsAreNull()
        {
            var options = new NavigationOptions();
            Assert.IsNull(options.RenderOverride);
            Assert.IsNull(options.CloseAllViews);
        }

        [Test]
        public void NavigationPoint_Dispose_SetsDisposedToTrue()
        {
            NullView view = new NullView();
            NullViewController controller = new NullViewController();
            NavigationOptions options = new NavigationOptions();
            NavigationPoint point = new NavigationPoint(view, controller, null, false, options);
            Assert.IsFalse(point.Disposed);
            point.Dispose();
            Assert.IsTrue(point.Disposed);
        }

        [Test]
        public void NavigationPoint_Constructor_StoresIsSceneView()
        {
            NullView view = new NullView();
            NullViewController controller = new NullViewController();
            NavigationOptions options = new NavigationOptions();
            NavigationPoint point = new NavigationPoint(view, controller, null, true, options);
            Assert.IsTrue(point.IsSceneView);
        }

        private class NullView : IView
        {
            public GameObject gameObject => null;
            public ViewState State => ViewState.Closed;
            public ViewType Type => ViewType.Screen;
            public void Bind(IViewController controller) { }
            public void Close() { }
            public void Focus() { }
            public void Hide() { }
            public void Open() { }
            public void Order(int depth) { }
        }

        private class NullViewController : IViewController
        {
            public void Bind(INavigation navigation) { }
            public void Close() { }
        }
    }
}
