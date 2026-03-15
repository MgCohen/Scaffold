using CommunityToolkit.Mvvm.ComponentModel;
using NUnit.Framework;
using Scaffold.MVVM.Binding;
using Scaffold.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

#pragma warning disable SCA0003
#pragma warning disable SCA0005
#pragma warning disable SCA0006

namespace Scaffold.MVVM.Tests
{
    public class MVVMTests
    {
        [Test]
        public void ViewModel_Bind_UpdatesValueFromNestedModel()
        {
            SampleViewModel viewModel = new SampleViewModel();
            viewModel.Bind(null);
            viewModel.RegisterNestedForTest();
            viewModel.SampleModel.Value = 42;
            Assert.AreEqual(42, viewModel.Value);
        }

        [Test]
        public void View_Bind_SetsInitialValue_FromViewModel()
        {
            SampleViewModel viewModel = new SampleViewModel();
            viewModel.SampleModel.Value = 3;
            using ViewFixture fixture = CreateViewFixture(viewModel);
            Assert.AreEqual(3, fixture.View.LastValue);
        }

        [Test]
        public void View_Bind_UpdatesTarget_WhenViewModelChanges()
        {
            using ViewFixture fixture = CreateViewFixture();
            fixture.ViewModel.SampleModel.Value = 7;
            Assert.AreEqual(7, fixture.View.LastValue);
        }

        [Test]
        public void ViewModel_Close_DelegatesToNavigation()
        {
            SpyNavigation navigation = new SpyNavigation();
            ClosableViewModel viewModel = new ClosableViewModel();
            viewModel.Bind(navigation);
            viewModel.Close();
            Assert.AreEqual(1, navigation.CloseCalls);
            Assert.AreSame(viewModel, navigation.LastClosedController);
            Assert.AreEqual(1, viewModel.OnClosedCalls);
        }

        [Test]
        public void View_Bind_WithWrongControllerType_ThrowsExpectedMessage()
        {
            GameObject gameObject = new GameObject(nameof(SampleView));
            try
            {
                SampleView view = gameObject.AddComponent<SampleView>();
                Assert.Throws<Exception>(() => view.Bind(new DifferentViewModel()));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ViewElement_Bind_FirstTime_WithNullController_DoesNotInvokeUnbind()
        {
            GameObject gameObject = new GameObject(nameof(GuardedViewElement));
            try
            {
                GuardedViewElement view = gameObject.AddComponent<GuardedViewElement>();
                Assert.DoesNotThrow(() => view.Bind(null));
                Assert.IsFalse(view.UnbindCalled);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void View_Rebind_SameViewModel_DoesNotDuplicatePropertyChangedSubscription()
        {
            using CountingViewFixture fixture = CreateCountingViewFixture();
            fixture.View.Bind(fixture.ViewModel);
            fixture.View.ResetNotifications();
            fixture.ViewModel.SampleModel.Value = 17;
            Assert.AreEqual(1, fixture.View.ValueChangedNotifications);
            Assert.AreEqual(17, fixture.View.LastValue);
        }

        [Test]
        public void View_Lifecycle_OpenCloseFocusHide_UpdatesStateAndHooks()
        {
            using LifecycleViewFixture fixture = CreateLifecycleViewFixture();
            IView view = fixture.View;
            view.Open();
            Assert.AreEqual(ViewState.Open, view.State);
            Assert.AreEqual(1, fixture.View.OpenCalls);
            Assert.IsTrue(fixture.GameObject.activeSelf);
            view.Focus();
            Assert.AreEqual(1, fixture.View.FocusCalls);
            Assert.IsTrue(fixture.GameObject.activeSelf);
            view.Hide();
            Assert.AreEqual(1, fixture.View.HideCalls);
            Assert.IsFalse(fixture.GameObject.activeSelf);
            view.Close();
            Assert.AreEqual(ViewState.Closed, view.State);
            Assert.AreEqual(1, fixture.View.CloseCalls);
            Assert.IsFalse(fixture.GameObject.activeSelf);
        }

        [Test]
        public void TreeBinding_UpdateBind_UpdatesRegisteredTarget()
        {
            BindingState source = new BindingState { Value = 5 };
            BindingTarget target = new BindingTarget();
            TreeBinding bindings = new TreeBinding();
            bindings.RegisterBind(() => source.Value, () => target.Value);
            source.Value = 12;
            string key = ExpressionsUtility.GetPropertyName(() => source.Value);
            bindings.UpdateBind(key);
            Assert.AreEqual(12, target.Value);
        }

        [Test]
        public void TreeBinding_UpdateBind_WithParentPath_UpdatesNestedBinds()
        {
            BindingState source = new BindingState
            {
                Nested = new NestedBindingState
                {
                    Value = 9,
                    Label = "alpha",
                }
            };
            NestedBindingTarget target = new NestedBindingTarget();
            TreeBinding bindings = new TreeBinding();
            bindings.RegisterBind<int, int>(() => source.Nested.Value, (Action<int>)(value => { target.Value = value; }));
            bindings.RegisterBind<string, string>(() => source.Nested.Label, (Action<string>)(label => { target.Label = label; }));
            source.Nested.Value = 33;
            source.Nested.Label = "beta";
            string parentKey = ExpressionsUtility.GetPropertyName(() => source.Nested);
            bindings.UpdateBind(parentKey);
            Assert.AreEqual(33, target.Value);
            Assert.AreEqual("beta", target.Label);
        }

        [Test]
        public void TreeBinding_RegisterConverterAndAdapter_AppliesConverter()
        {
            BindingState source = new BindingState { Value = 10 };
            ConvertedBindingTarget target = new ConvertedBindingTarget();
            TreeBinding bindings = new TreeBinding();
            bindings.RegisterConverter<int, string>(new IncrementingStringConverter());
            bindings.RegisterAdapter<string>(new BracketAdapter());
            bindings.RegisterBind<int, string>(() => source.Value, (Action<string>)(value => { target.Value = value; }));
            source.Value = 15;
            string key = ExpressionsUtility.GetPropertyName(() => source.Value);
            bindings.UpdateBind(key);
            Assert.AreEqual("16", target.Value);
        }

        [Test]
        public void TreeBinding_RegisterAdapter_DoesNotBreakBindingPipeline()
        {
            AdaptableBindingState source = new AdaptableBindingState { Label = "hello" };
            AdaptableBindingTarget target = new AdaptableBindingTarget();
            TreeBinding bindings = new TreeBinding();
            bindings.RegisterAdapter<string>(new BracketAdapter());
            bindings.RegisterBind(() => source.Label, () => target.Label);
            source.Label = "world";
            string key = ExpressionsUtility.GetPropertyName(() => source.Label);
            bindings.UpdateBind(key);
            Assert.AreEqual("world", target.Label);
        }

        [Test]
        public void TreeBinding_Unbind_ClearsRegisteredContexts()
        {
            BindingState source = new BindingState { Value = 1 };
            BindingTarget target = new BindingTarget();
            TreeBinding bindings = new TreeBinding();
            bindings.RegisterBind(() => source.Value, () => target.Value);
            string key = ExpressionsUtility.GetPropertyName(() => source.Value);
            source.Value = 3;
            bindings.UpdateBind(key);
            Assert.AreEqual(3, target.Value);
            bindings.Unbind();
            source.Value = 8;
            bindings.UpdateBind(key);
            Assert.AreEqual(3, target.Value);
        }

        [Test]
        public void TreeBinding_RegisterBindCollection_TracksAddAndRemove()
        {
            CollectionBindingState source = new CollectionBindingState();
            source.Items.Add(1);
            source.Items.Add(2);
            TrackingCollectionHandler handler = new TrackingCollectionHandler();
            TreeBinding bindings = new TreeBinding();
            bindings.RegisterBindCollection(() => source.Items, handler);
            string key = ExpressionsUtility.GetPropertyName(() => source.Items);
            bindings.UpdateBind(key);
            Assert.AreEqual(2, handler.AddCalls);
            source.Items.Add(3);
            Assert.AreEqual(3, handler.AddCalls);
            source.Items.Remove(2);
            Assert.AreEqual(1, handler.RemoveCalls);
        }

        [Test]
        public void TreeBinding_RegisterBind_DefaultStrictMode_ThrowsForUnresolvedDeferredChain()
        {
            DeferredBindingState source = new DeferredBindingState();
            BindingTarget target = new BindingTarget();
            TreeBinding bindings = new TreeBinding();
            Assert.Throws<NullReferenceException>(() => bindings.RegisterBind<int, int>(() => source.LateInstance.Value, value => target.Value = value));
        }

        [Test]
        public void TreeBinding_RegisterBind_LazyMode_DefersEvaluationUntilChainResolves()
        {
            DeferredBindingState source = new DeferredBindingState();
            int updateCalls = 0;
            int lastValue = -1;
            TreeBinding bindings = new TreeBinding();
            bindings.RegisterBind<int, int>(() => source.LateInstance.Value, value =>
            {
                updateCalls++;
                lastValue = value;
            }, BindingOptions.Lazy);

            Assert.AreEqual(0, updateCalls);

            string deferredKey = ExpressionsUtility.GetPropertyName(() => source.LateInstance);
            Assert.DoesNotThrow(() => bindings.UpdateBind(deferredKey));
            Assert.AreEqual(0, updateCalls);

            source.LateInstance = new DeferredNestedBindingState { Value = 23 };
            bindings.UpdateBind(deferredKey);
            Assert.AreEqual(1, updateCalls);
            Assert.AreEqual(23, lastValue);
        }

        [Test]
        public void TreeBinding_DisposePropertyBinding_DetachesOnlyDisposedBinding()
        {
            BindingState source = new BindingState { Value = 1 };
            BindingTarget firstTarget = new BindingTarget();
            BindingTarget secondTarget = new BindingTarget();
            TreeBinding bindings = new TreeBinding();

            IBindedProperty<int, int> first = bindings.RegisterBind(() => source.Value, () => firstTarget.Value);
            IBindedProperty<int, int> second = bindings.RegisterBind(() => source.Value, () => secondTarget.Value);
            string key = ExpressionsUtility.GetPropertyName(() => source.Value);

            source.Value = 4;
            bindings.UpdateBind(key);
            Assert.AreEqual(4, firstTarget.Value);
            Assert.AreEqual(4, secondTarget.Value);

            first.Dispose();
            Assert.DoesNotThrow(() => first.Dispose());

            source.Value = 9;
            bindings.UpdateBind(key);
            Assert.AreEqual(4, firstTarget.Value);
            Assert.AreEqual(9, secondTarget.Value);

            second.Dispose();
            Assert.DoesNotThrow(() => bindings.UpdateBind(key));
        }

        [Test]
        public void TreeBinding_DisposeCollectionBinding_DetachesAndStopsFutureCollectionUpdates()
        {
            CollectionBindingState source = new CollectionBindingState();
            source.Items.Add(1);
            source.Items.Add(2);
            TrackingCollectionHandler handler = new TrackingCollectionHandler();
            TreeBinding bindings = new TreeBinding();

            IBindedCollection<int, string> collectionBind = bindings.RegisterBindCollection(() => source.Items, handler);
            string key = ExpressionsUtility.GetPropertyName(() => source.Items);
            bindings.UpdateBind(key);
            Assert.AreEqual(2, handler.AddCalls);

            collectionBind.Dispose();
            Assert.DoesNotThrow(() => collectionBind.Dispose());

            source.Items.Add(3);
            source.Items.Remove(1);
            Assert.AreEqual(2, handler.AddCalls);
            Assert.AreEqual(0, handler.RemoveCalls);
            Assert.DoesNotThrow(() => bindings.UpdateBind(key));
        }

        [Test]
        public void TreeBinding_DisposedLazyBinding_DoesNotUpdateAfterDeferredChainResolves()
        {
            DeferredBindingState source = new DeferredBindingState();
            int updateCalls = 0;
            TreeBinding bindings = new TreeBinding();

            IBindedProperty<int, int> bind = bindings.RegisterBind<int, int>(() => source.LateInstance.Value, value => updateCalls++, BindingOptions.Lazy);
            bind.Dispose();

            source.LateInstance = new DeferredNestedBindingState { Value = 30 };
            string deferredKey = ExpressionsUtility.GetPropertyName(() => source.LateInstance);
            bindings.UpdateBind(deferredKey);
            Assert.AreEqual(0, updateCalls);
        }

        [Test]
        public void BindSource_AttributeOnlyClass_ImplementsInterfaceAndBinds()
        {
            GeneratedBindSourceProbe probe = new GeneratedBindSourceProbe
            {
                SourceValue = 4
            };

            IBindSource bindSource = (IBindSource)probe;
            bindSource.Bind<int, int>(() => probe.SourceValue, () => probe.TargetValue);
            probe.SourceValue = 11;
            bindSource.UpdateBinding(ExpressionsUtility.GetPropertyName(() => probe.SourceValue));

            Assert.AreEqual(11, probe.TargetValue);
        }

        [Test]
        public void BindSource_BindCollection_ReturnsDisposableHandle()
        {
            GeneratedBindSourceCollectionProbe probe = new GeneratedBindSourceCollectionProbe();
            probe.SourceValues.Add(7);
            IBindSource bindSource = (IBindSource)probe;
            TrackingCollectionHandler handler = new TrackingCollectionHandler();

            IBindedCollection<int, string> handle = bindSource.BindCollection(() => probe.SourceValues, handler);
            Assert.NotNull(handle);
            bindSource.UpdateBinding(ExpressionsUtility.GetPropertyName(() => probe.SourceValues));
            Assert.AreEqual(1, handler.AddCalls);

            handle.Dispose();
        }

        [Test]
        public void EventLedger_Raise_BubblesFromChildToRoot()
        {
            using HierarchyFixture fixture = CreateHierarchyFixture();
            EventLedger<TestViewEvent> ledger = new EventLedger<TestViewEvent>();
            List<Transform> callOrder = new List<Transform>();
            ledger.Register(fixture.Child.transform, evt => callOrder.Add(fixture.Child.transform));
            ledger.Register(fixture.Parent.transform, evt => callOrder.Add(fixture.Parent.transform));
            ledger.Register(fixture.Root.transform, evt => callOrder.Add(fixture.Root.transform));
            ledger.Raise(fixture.Child.transform, new TestViewEvent());
            CollectionAssert.AreEqual(new[] { fixture.Child.transform, fixture.Parent.transform, fixture.Root.transform }, callOrder);
        }

        [Test]
        public void EventLedger_Consume_StopsPropagation()
        {
            using HierarchyFixture fixture = CreateHierarchyFixture();
            EventLedger<TestViewEvent> ledger = new EventLedger<TestViewEvent>();
            bool rootCalled = false;
            ledger.Register(fixture.Parent.transform, evt => evt.Consume());
            ledger.Register(fixture.Root.transform, evt => rootCalled = true);
            ledger.Raise(fixture.Child.transform, new TestViewEvent());
            Assert.IsFalse(rootCalled);
        }

        [Test]
        public void EventLedger_Unregister_RemovesCallback()
        {
            using HierarchyFixture fixture = CreateHierarchyFixture();
            EventLedger<TestViewEvent> ledger = new EventLedger<TestViewEvent>();
            int count = 0;
            Action<TestViewEvent> callback = evt => count++;
            ledger.Register(fixture.Child.transform, callback);
            ledger.Unregister(fixture.Child.transform, callback);
            ledger.Raise(fixture.Child.transform, new TestViewEvent());
            Assert.AreEqual(0, count);
        }

        [Test]
        public void EventLedger_RaiseWrongType_Throws()
        {
            using HierarchyFixture fixture = CreateHierarchyFixture();
            IEventLedger ledger = new EventLedger<TestViewEvent>();
            Assert.Throws<Exception>(() => ledger.Raise(fixture.Child.transform, new OtherViewEvent()));
        }

        [Test]
        public void EventLedger_CallbackException_DoesNotStopOtherCallbacks()
        {
            using HierarchyFixture fixture = CreateHierarchyFixture();
            EventLedger<TestViewEvent> ledger = new EventLedger<TestViewEvent>();
            bool called = false;
            using IDisposable _ = ViewEvents.PushExceptionOptions(new EventLedgerExceptionOptions(EventLedgerExceptionMode.ReportAndContinue, null));
            ledger.Register(fixture.Child.transform, evt => throw new InvalidOperationException("boom"));
            ledger.Register(fixture.Child.transform, evt => called = true);
            ledger.Raise(fixture.Child.transform, new TestViewEvent());
            Assert.IsTrue(called);
        }

        [Test]
        public void EventLedger_DefaultExceptionOptions_AreConfiguredForReportAndContinue()
        {
            EventLedgerExceptionOptions options = ViewEvents.GetExceptionOptions();
            Assert.AreEqual(EventLedgerExceptionMode.ReportAndContinue, options.Mode);
            Assert.IsNotNull(options.Reporter);
        }

        [Test]
        public void EventLedger_ReportAndContinue_CapturesCallbackExceptionsWithoutThrowing()
        {
            using HierarchyFixture fixture = CreateHierarchyFixture();
            EventLedger<TestViewEvent> ledger = new EventLedger<TestViewEvent>();
            List<Exception> reported = new List<Exception>();
            bool called = false;
            using IDisposable _ = ViewEvents.PushExceptionOptions(new EventLedgerExceptionOptions(EventLedgerExceptionMode.ReportAndContinue, (ex, _) => reported.Add(ex)));
            ledger.Register(fixture.Child.transform, evt => throw new InvalidOperationException("boom"));
            ledger.Register(fixture.Child.transform, evt => called = true);
            Assert.DoesNotThrow(() => ledger.Raise(fixture.Child.transform, new TestViewEvent()));
            Assert.IsTrue(called);
            Assert.AreEqual(1, reported.Count);
            Assert.IsInstanceOf<InvalidOperationException>(reported[0]);
        }

        [Test]
        public void EventLedger_ThrowAfterDispatch_ThrowsAggregateWithoutBreakingCallbackChain()
        {
            using HierarchyFixture fixture = CreateHierarchyFixture();
            EventLedger<TestViewEvent> ledger = new EventLedger<TestViewEvent>();
            bool called = false;
            using IDisposable _ = ViewEvents.PushExceptionOptions(new EventLedgerExceptionOptions(EventLedgerExceptionMode.ThrowAfterDispatch, null));
            ledger.Register(fixture.Child.transform, evt => throw new InvalidOperationException("boom"));
            ledger.Register(fixture.Child.transform, evt => called = true);
            AggregateException exception = Assert.Throws<AggregateException>(() => ledger.Raise(fixture.Child.transform, new TestViewEvent()));
            Assert.IsTrue(called);
            Assert.NotNull(exception);
            Assert.AreEqual(1, exception.InnerExceptions.Count);
            Assert.IsInstanceOf<InvalidOperationException>(exception.InnerExceptions[0]);
        }

        [Test]
        public void ViewEvents_PushExceptionOptions_RestoresPreviousOptionsOnDispose()
        {
            EventLedgerExceptionOptions previous = ViewEvents.GetExceptionOptions();
            using (ViewEvents.PushExceptionOptions(new EventLedgerExceptionOptions(EventLedgerExceptionMode.ThrowAfterDispatch, null)))
            {
                EventLedgerExceptionOptions active = ViewEvents.GetExceptionOptions();
                Assert.AreEqual(EventLedgerExceptionMode.ThrowAfterDispatch, active.Mode);
                Assert.IsNull(active.Reporter);
            }

            EventLedgerExceptionOptions restored = ViewEvents.GetExceptionOptions();
            Assert.AreSame(previous, restored);
        }

        [Test]
        public void ViewEvents_RegisterRaiseUnregister_TypedCallbackFlow()
        {
            using ListenerFixture fixture = CreateListenerFixture();
            int count = 0;
            Action<TestViewEvent> callback = evt => count++;
            ViewEvents.Register(fixture.Listener, callback);
            ViewEvents.Raise(fixture.Source, new TestViewEvent());
            ViewEvents.Unregister(fixture.Listener, callback);
            ViewEvents.Raise(fixture.Source, new TestViewEvent());
            Assert.AreEqual(1, count);
        }

        [Test]
        public void ViewEvents_RegisterRaiseUnregister_OpenTypeCallbackFlow()
        {
            using ListenerFixture fixture = CreateListenerFixture();
            int count = 0;
            Action<ViewEvent> callback = evt => count++;
            ViewEvents.Register(typeof(TestViewEvent), fixture.Listener, callback);
            ViewEvents.Raise(fixture.Source, new TestViewEvent());
            ViewEvents.Unregister(typeof(TestViewEvent), fixture.Listener, callback);
            ViewEvents.Raise(fixture.Source, new TestViewEvent());
            Assert.AreEqual(1, count);
        }

        private static ViewFixture CreateViewFixture()
        {
            SampleViewModel viewModel = new SampleViewModel();
            return CreateViewFixture(viewModel);
        }

        private static ViewFixture CreateViewFixture(SampleViewModel viewModel)
        {
            viewModel.Bind(null);
            GameObject gameObject = new GameObject(nameof(SampleView));
            SampleView view = gameObject.AddComponent<SampleView>();
            view.Bind(viewModel);
            return new ViewFixture(gameObject, view, viewModel);
        }

        private static CountingViewFixture CreateCountingViewFixture()
        {
            SampleViewModel viewModel = new SampleViewModel();
            viewModel.Bind(null);
            GameObject gameObject = new GameObject(nameof(CountingSampleView));
            CountingSampleView view = gameObject.AddComponent<CountingSampleView>();
            view.Bind(viewModel);
            return new CountingViewFixture(gameObject, view, viewModel);
        }

        private static LifecycleViewFixture CreateLifecycleViewFixture()
        {
            GameObject gameObject = new GameObject(nameof(TrackingLifecycleView));
            TrackingLifecycleView view = gameObject.AddComponent<TrackingLifecycleView>();
            view.Bind(new SampleViewModel());
            return new LifecycleViewFixture(gameObject, view);
        }

        private static HierarchyFixture CreateHierarchyFixture()
        {
            GameObject root = new GameObject("Root");
            GameObject parent = new GameObject("Parent");
            GameObject child = new GameObject("Child");
            parent.transform.SetParent(root.transform, false);
            child.transform.SetParent(parent.transform, false);
            return new HierarchyFixture(root, parent, child);
        }

        private static ListenerFixture CreateListenerFixture()
        {
            HierarchyFixture hierarchy = CreateHierarchyFixture();
            ListenerBehaviour source = hierarchy.Child.AddComponent<ListenerBehaviour>();
            ListenerBehaviour listener = hierarchy.Root.AddComponent<ListenerBehaviour>();
            return new ListenerFixture(hierarchy, source, listener);
        }

        private sealed class ViewFixture : System.IDisposable
        {
            public ViewFixture(GameObject gameObject, SampleView view, SampleViewModel viewModel)
            {
                GameObject = gameObject;
                View = view;
                ViewModel = viewModel;
            }

            public GameObject GameObject { get; }
            public SampleView View { get; }
            public SampleViewModel ViewModel { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(GameObject);
            }
        }

        private sealed class CountingViewFixture : IDisposable
        {
            public CountingViewFixture(GameObject gameObject, CountingSampleView view, SampleViewModel viewModel)
            {
                GameObject = gameObject;
                View = view;
                ViewModel = viewModel;
            }

            public GameObject GameObject { get; }
            public CountingSampleView View { get; }
            public SampleViewModel ViewModel { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(GameObject);
            }
        }

        private sealed class LifecycleViewFixture : IDisposable
        {
            public LifecycleViewFixture(GameObject gameObject, TrackingLifecycleView view)
            {
                GameObject = gameObject;
                View = view;
            }

            public GameObject GameObject { get; }
            public TrackingLifecycleView View { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(GameObject);
            }
        }

        private sealed class HierarchyFixture : IDisposable
        {
            public HierarchyFixture(GameObject root, GameObject parent, GameObject child)
            {
                Root = root;
                Parent = parent;
                Child = child;
            }

            public GameObject Root { get; }
            public GameObject Parent { get; }
            public GameObject Child { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Child);
                UnityEngine.Object.DestroyImmediate(Parent);
                UnityEngine.Object.DestroyImmediate(Root);
            }
        }

        private sealed class ListenerFixture : IDisposable
        {
            public ListenerFixture(HierarchyFixture hierarchy, ListenerBehaviour source, ListenerBehaviour listener)
            {
                hierarchyFixture = hierarchy;
                Source = source;
                Listener = listener;
            }

            private readonly HierarchyFixture hierarchyFixture;

            public ListenerBehaviour Source { get; }
            public ListenerBehaviour Listener { get; }

            public void Dispose()
            {
                hierarchyFixture.Dispose();
            }
        }
    }

    public partial class SampleModel : Model
    {
        [ObservableProperty]
        private int value;
    }

    public partial class SampleViewModel : ViewModel
    {
        [ObservableProperty]
        private SampleModel sampleModel = new SampleModel();

        [ObservableProperty]
        private int value;

        protected override void Initialize()
        {
            Bind(() => SampleModel.Value, () => Value);
            Value = SampleModel.Value;
        }

        public void RegisterNestedForTest()
        {
            ((INestedObservableProperties)this).RegisterNestedProperties();
        }
    }

    public class SampleView : View<SampleViewModel>
    {
        public int LastValue { get; private set; }

        protected override void OnBind()
        {
            Bind<int, int>(() => viewModel.Value, OnValueChanged);
        }

        private void OnValueChanged(int value)
        {
            LastValue = value;
        }
    }

    public class GuardedViewElement : ViewElement<IViewModel>
    {
        public bool UnbindCalled { get; private set; }

        protected override void OnUnbind()
        {
            UnbindCalled = true;
            _ = viewModel.ToString();
        }
    }

    public class CountingSampleView : View<SampleViewModel>
    {
        public int LastValue { get; private set; }
        public int ValueChangedNotifications { get; private set; }

        protected override void OnBind()
        {
            Bind<int, int>(() => viewModel.Value, OnValueChanged);
        }

        private void OnValueChanged(int value)
        {
            LastValue = value;
            ValueChangedNotifications++;
        }

        public void ResetNotifications()
        {
            ValueChangedNotifications = 0;
        }
    }

    public partial class DifferentModel : Model
    {
        [ObservableProperty]
        private int value;
    }

    public partial class DifferentViewModel : ViewModel
    {
        [ObservableProperty]
        private DifferentModel differentModel = new DifferentModel();

        [ObservableProperty]
        private int value;

        protected override void Initialize()
        {
            Bind(() => DifferentModel.Value, () => Value);
        }
    }

    public partial class ClosableViewModel : ViewModel
    {
        public int OnClosedCalls { get; private set; }

        protected override void OnClosed()
        {
            OnClosedCalls++;
            base.OnClosed();
        }
    }

    public class TrackingLifecycleView : View<SampleViewModel>
    {
        public int OpenCalls { get; private set; }
        public int CloseCalls { get; private set; }
        public int FocusCalls { get; private set; }
        public int HideCalls { get; private set; }

        protected override void OnOpen()
        {
            OpenCalls++;
            base.OnOpen();
        }

        protected override void OnClose()
        {
            CloseCalls++;
            base.OnClose();
        }

        protected override void OnFocus()
        {
            FocusCalls++;
            base.OnFocus();
        }

        protected override void OnHide()
        {
            HideCalls++;
            base.OnHide();
        }
    }

    public class BindingState
    {
        public int Value { get; set; }
        public NestedBindingState Nested { get; set; } = new NestedBindingState();
    }

    public class DeferredBindingState
    {
        public DeferredNestedBindingState LateInstance { get; set; }
    }

    public class DeferredNestedBindingState
    {
        public int Value { get; set; }
    }

    public class NestedBindingState
    {
        public int Value { get; set; }
        public string Label { get; set; }
    }

    public class CollectionBindingState
    {
        public ObservableCollection<int> Items { get; } = new ObservableCollection<int>();
    }

    public class AdaptableBindingState
    {
        public string Label { get; set; }
    }

    public class BindingTarget
    {
        public int Value { get; set; }
    }

    public class NestedBindingTarget
    {
        public int Value { get; set; }
        public string Label { get; set; }
    }

    public class ConvertedBindingTarget
    {
        public string Value { get; set; }
    }

    public class AdaptableBindingTarget
    {
        public string Label { get; set; }
    }

    [BindSource(typeof(TreeBinding))]
    public partial class GeneratedBindSourceProbe
    {
        public int SourceValue { get; set; }
        public int TargetValue { get; set; }
    }

    [BindSource(typeof(TreeBinding))]
    public partial class GeneratedBindSourceCollectionProbe
    {
        public ObservableCollection<int> SourceValues { get; } = new ObservableCollection<int>();
    }

    public class IncrementingStringConverter : Scaffold.MVVM.Binding.Converter<int, string>
    {
        public override string Convert(int source)
        {
            return (source + 1).ToString();
        }
    }

    public class BracketAdapter : Adapter<string>
    {
        public override string Resolve(string target)
        {
            return $"[{target}]";
        }
    }

    public class TrackingCollectionHandler : ICollectionHandler<int, string>
    {
        public int AddCalls { get; private set; }
        public int RemoveCalls { get; private set; }

        public readonly List<string> Values = new List<string>();

        public string Add(int source)
        {
            AddCalls++;
            string value = source.ToString();
            Values.Add(value);
            return value;
        }

        public void Remove(string item)
        {
            RemoveCalls++;
            Values.Remove(item);
        }
    }

    public class SpyNavigation : INavigation
    {
        public int CloseCalls { get; private set; }
        public IViewController LastClosedController { get; private set; }

        public NavigationPoint CurrentPoint => null;

        public void Open<TViewController>(TViewController controller, bool closeCurrent = false, NavigationOptions options = null) where TViewController : IViewController
        {
        }

        public void Close<TViewController>(TViewController controller) where TViewController : IViewController
        {
            CloseCalls++;
            LastClosedController = controller;
        }

        public IViewController Return()
        {
            return null;
        }
    }

    public class TestViewEvent : ViewEvent
    {
    }

    public class OtherViewEvent : ViewEvent
    {
    }

    public class ListenerBehaviour : MonoBehaviour
    {
    }
}

#pragma warning restore SCA0006
#pragma warning restore SCA0005
#pragma warning restore SCA0003
