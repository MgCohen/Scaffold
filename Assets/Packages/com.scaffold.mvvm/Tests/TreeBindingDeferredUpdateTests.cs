using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using NUnit.Framework;
using Scaffold.MVVM.Binding;

namespace Scaffold.MVVM.Tests
{
    public sealed class TreeBindingDeferredUpdateTests
    {
        private sealed class TestVm
        {
            public string Value { get; set; }
        }

        private sealed class TestDeferredBindingScheduler : IDeferredBindingScheduler
        {
            private readonly Queue<Action> queue = new Queue<Action>();

            public void Schedule(Action continuation)
            {
                if (continuation is null)
                {
                    throw new ArgumentNullException(nameof(continuation));
                }
                queue.Enqueue(continuation);
            }

            public void Drain()
            {
                while (queue.Count > 0)
                {
                    queue.Dequeue().Invoke();
                }
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
            tree.RegisterBindingUpdatePolicy(BindingUpdateTiming.Immediate, null);
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
            var sched = new TestDeferredBindingScheduler();
            var tree = new TreeBinding();
            tree.RegisterBindingUpdatePolicy(BindingUpdateTiming.NextFrame, sched);
            tree.RegisterBind(valueExpr, new Action<string>(_ => hits++), BindingOptions.Lazy);
            tree.UpdateBind(bindKey);
            tree.UpdateBind(bindKey);
            tree.UpdateBind(bindKey);
            Assert.AreEqual(0, hits);
            sched.Drain();
            Assert.AreEqual(1, hits);
        }

        [Test]
        public void DeferredDefault_WithImmediatePerBindOverride_InvokesTargetEachTime()
        {
            int hits = 0;
            var vm = new TestVm { Value = "a" };
            Expression<Func<string>> valueExpr = () => vm.Value;
            string bindKey = valueExpr.GetPropertyName();
            var sched = new TestDeferredBindingScheduler();
            var tree = new TreeBinding();
            tree.RegisterBindingUpdatePolicy(BindingUpdateTiming.NextFrame, sched);
            tree.RegisterBind(valueExpr, new Action<string>(_ => hits++), BindingOptions.StrictImmediate);
            tree.UpdateBind(bindKey);
            tree.UpdateBind(bindKey);
            Assert.AreEqual(3, hits);
            sched.Drain();
            Assert.AreEqual(3, hits);
        }

        [Test]
        public void ImmediateDefault_WithDeferredPerBind_RequiresScheduler()
        {
            int hits = 0;
            var vm = new TestVm { Value = "a" };
            Expression<Func<string>> valueExpr = () => vm.Value;
            string bindKey = valueExpr.GetPropertyName();
            var sched = new TestDeferredBindingScheduler();
            var tree = new TreeBinding();
            tree.RegisterBindingUpdatePolicy(BindingUpdateTiming.Immediate, sched);
            tree.RegisterBind(valueExpr, new Action<string>(_ => hits++), new BindingOptions(true, BindingUpdateTiming.NextFrame));
            tree.UpdateBind(bindKey);
            tree.UpdateBind(bindKey);
            Assert.AreEqual(0, hits);
            sched.Drain();
            Assert.AreEqual(1, hits);
        }
    }
}
