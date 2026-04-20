using NUnit.Framework;
using Scaffold.MVVM;
using Scaffold.MVVM.Contracts;
using Scaffold.Navigation.Contracts;
using UnityEngine;

namespace Scaffold.MVVM.View.Tests
{
    public sealed class ViewLifecycleAndContextTests
    {
        private sealed class TestVm : ViewModel
        {
        }

        private sealed class LifecycleView : View<TestVm>
        {
            public bool LastWasHidden;
            public bool? LastHiding;

            protected override void OnOpen(bool wasHidden)
            {
                LastWasHidden = wasHidden;
            }

            protected override void OnClose(bool hiding)
            {
                LastHiding = hiding;
            }
        }

        private sealed class AutoBindRoot : View<TestVm>
        {
            protected override bool AutoBindChildViewComponents => true;
        }

        private sealed class LeafPanel : ViewElement<TestVm>
        {
            public int BindCount;

            protected override void OnBind()
            {
                BindCount++;
            }
        }

        [Test]
        public void ViewLifecycle_Open_FocusAfterHide_Close_UsesFlags()
        {
            var go = new GameObject("v");
            var view = go.AddComponent<LifecycleView>();
            var navView = (Scaffold.Navigation.Contracts.IView)view;

            navView.Open();
            Assert.That(view.LastWasHidden, Is.False);
            Assert.That(view.LastHiding, Is.Null);

            navView.Hide();
            Assert.That(view.LastHiding, Is.True);

            navView.Focus();
            Assert.That(view.LastWasHidden, Is.True);

            navView.Close();
            Assert.That(view.LastHiding, Is.False);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void AutoBindChildViewComponents_BindsMatchingChildViewElements()
        {
            var rootGo = new GameObject("root");
            var childGo = new GameObject("child");
            childGo.transform.SetParent(rootGo.transform, false);
            var root = rootGo.AddComponent<AutoBindRoot>();
            var leaf = childGo.AddComponent<LeafPanel>();
            var vm = new TestVm();
            root.Bind(vm);
            Assert.That(leaf.BindCount, Is.EqualTo(1));
            Object.DestroyImmediate(rootGo);
        }

        [Test]
        public void ViewContext_Register_And_TryResolve()
        {
            var go = new GameObject("ctx");
            var view = go.AddComponent<LifecycleView>();
            var registry = (IViewContext)view.Context;
            registry.Register<ISampleService>(new SampleService());
            Assert.That(registry.TryResolve(out ISampleService s), Is.True);
            Assert.That(s, Is.Not.Null);
            Object.DestroyImmediate(go);
        }

        private interface ISampleService
        {
        }

        private sealed class SampleService : ISampleService
        {
        }
    }
}
