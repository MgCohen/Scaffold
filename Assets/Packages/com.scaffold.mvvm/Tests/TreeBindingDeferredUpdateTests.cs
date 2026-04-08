using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using NUnit.Framework;
using Scaffold.MVVM.Binding;

namespace Scaffold.MVVM.Tests
{
    public sealed class TreeBindingDeferredUpdateTests
    {
        private readonly Queue<Action> scheduleQueue = new Queue<Action>();

        private sealed class TestVm
        {
            public string Value { get; set; }
        }

        [SetUp]
        public void SetUp()
        {
            scheduleQueue.Clear();
            DeferredBindingCoroutineHost.ScheduleCore = (action, timing) =>
            {
                if (action is null)
                {
                    throw new ArgumentNullException(nameof(action));
                }

                scheduleQueue.Enqueue(action);
            };
        }

        [TearDown]
        public void TearDown()
        {
            DeferredBindingCoroutineHost.ResetScheduleCoreForTests();
        }

        private void Drain()
        {
            while (scheduleQueue.Count > 0)
            {
                scheduleQueue.Dequeue().Invoke();
            }
        }

        [Test]
        public void ImmediatePolicy_MultipleUpdateBindCalls_InvokesTargetEachTime()
        {
            int hits = 0;
            var vm = new TestVm { Value = "a" };
            Expression<Func<string>> valueExpr = () => vm.Value;
            string bindKey = valueExpr.GetPropertyName();
            var tree = new TreeBinding();
            tree.RegisterBindingUpdatePolicy(BindingUpdateTiming.Immediate);
            tree.RegisterBind(valueExpr, new Action<string>(_ => hits++), BindingOptions.Lazy);
            tree.UpdateBind(bindKey);
            tree.UpdateBind(bindKey);
            tree.UpdateBind(bindKey);
            Assert.AreEqual(3, hits);
        }

        [Test]
        public void DeferredPolicy_MultipleUpdateBindCallsBeforeDrain_InvokesTargetOnce()
        {
            int hits = 0;
            var vm = new TestVm { Value = "a" };
            Expression<Func<string>> valueExpr = () => vm.Value;
            string bindKey = valueExpr.GetPropertyName();
            var tree = new TreeBinding();
            tree.RegisterBindingUpdatePolicy(BindingUpdateTiming.NextFrame);
            tree.RegisterBind(valueExpr, new Action<string>(_ => hits++), BindingOptions.Lazy);
            tree.UpdateBind(bindKey);
            tree.UpdateBind(bindKey);
            tree.UpdateBind(bindKey);
            Assert.AreEqual(0, hits);
            Drain();
            Assert.AreEqual(1, hits);
        }

        [Test]
        public void DeferredDefault_WithImmediatePerBindOverride_InvokesTargetEachTime()
        {
            int hits = 0;
            var vm = new TestVm { Value = "a" };
            Expression<Func<string>> valueExpr = () => vm.Value;
            string bindKey = valueExpr.GetPropertyName();
            var tree = new TreeBinding();
            tree.RegisterBindingUpdatePolicy(BindingUpdateTiming.NextFrame);
            // Lazy skips initial apply; per-bind Immediate still runs each UpdateBind synchronously (not deferred batch).
            tree.RegisterBind(valueExpr, new Action<string>(_ => hits++), new BindingOptions(true, BindingUpdateTiming.Immediate));
            tree.UpdateBind(bindKey);
            tree.UpdateBind(bindKey);
            Assert.AreEqual(2, hits);
            Drain();
            Assert.AreEqual(2, hits);
        }

        [Test]
        public void ImmediateDefault_WithDeferredPerBind_InvokesAfterDrain()
        {
            int hits = 0;
            var vm = new TestVm { Value = "a" };
            Expression<Func<string>> valueExpr = () => vm.Value;
            string bindKey = valueExpr.GetPropertyName();
            var tree = new TreeBinding();
            tree.RegisterBindingUpdatePolicy(BindingUpdateTiming.Immediate);
            tree.RegisterBind(valueExpr, new Action<string>(_ => hits++), new BindingOptions(true, BindingUpdateTiming.NextFrame));
            tree.UpdateBind(bindKey);
            tree.UpdateBind(bindKey);
            Assert.AreEqual(0, hits);
            Drain();
            Assert.AreEqual(1, hits);
        }
    }
}
