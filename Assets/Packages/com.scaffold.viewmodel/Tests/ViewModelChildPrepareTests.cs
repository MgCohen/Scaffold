using NUnit.Framework;
using Scaffold.MVVM.Contracts;
using Scaffold.Navigation.Contracts;

namespace Scaffold.MVVM.VmComposition.Tests
{
    public sealed class ViewModelChildPrepareTests
    {
        private sealed class RecordingNavigation : INavigation
        {
            public int PrepareCalls;
            public IViewController CurrentController => null;

            public void Open<TViewController>(TViewController controller, NavigationOptions options) where TViewController : IViewController
            {
            }

            public void Open<TViewController>(TViewController controller, bool closeCurrent = false, NavigationOptions options = null) where TViewController : IViewController
            {
            }

            public void PrepareDependencies(IViewController controller)
            {
                PrepareCalls++;
            }

            public void Close<TViewController>(TViewController controller) where TViewController : IViewController
            {
            }

            public IViewController Return()
            {
                return null;
            }
        }

        private sealed class ChildVm : ViewModel
        {
        }

        private sealed class ParentVm : ViewModel
        {
            internal void TestBindChild(ChildVm child)
            {
                BindChildViewModel(child);
            }
        }

        [Test]
        public void BindChildViewModel_CallsPrepareDependenciesBeforeBind()
        {
            var nav = new RecordingNavigation();
            var parent = new ParentVm();
            parent.Bind(nav);
            var child = new ChildVm();
            parent.TestBindChild(child);
            Assert.That(nav.PrepareCalls, Is.EqualTo(1));
        }
    }
}
