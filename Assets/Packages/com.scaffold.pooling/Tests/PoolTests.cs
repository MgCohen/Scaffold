using System;
using NUnit.Framework;

namespace Scaffold.Pooling.Tests
{
    public sealed class PoolTests
    {
        [Test]
        public void TakeAndReturn_MovesBetweenActiveAndAvailable()
        {
            int serial = 0;
            Pool<int> pool = new Pool<int>(() => ++serial);

            Assert.That(pool.Available, Is.EqualTo(0));
            Assert.That(pool.Active.Count, Is.EqualTo(0));

            int a = pool.Take();
            Assert.That(a, Is.EqualTo(1));
            Assert.That(pool.Available, Is.EqualTo(0));
            Assert.That(pool.Active.Count, Is.EqualTo(1));

            pool.Return(a);
            Assert.That(pool.Available, Is.EqualTo(1));
            Assert.That(pool.Active.Count, Is.EqualTo(0));

            int b = pool.Take();
            Assert.That(b, Is.EqualTo(1));
            Assert.That(pool.Available, Is.EqualTo(0));
        }

        [Test]
        public void IPoolable_InvokesLifecycleAndReturnRequested()
        {
            PoolablePooled item = new PoolablePooled();
            Pool<PoolablePooled> pool = new Pool<PoolablePooled>(() => item);

            PoolablePooled taken = pool.Take();
            Assert.That(taken.TakenCount, Is.EqualTo(1));
            Assert.That(taken.ReturnedCount, Is.EqualTo(0));

            taken.RequestReturn();
            Assert.That(taken.ReturnedCount, Is.EqualTo(1));
            Assert.That(pool.Active.Count, Is.EqualTo(0));
            Assert.That(pool.Available, Is.EqualTo(1));
        }

        [Test]
        public void Return_WhenIdleCapReached_CallsOnDestroy()
        {
            int next = 0;
            int destroyed = 0;
            Pool<int> pool = new Pool<int>(
                () => next++,
                onDestroy: _ => destroyed++,
                initialSize: 0,
                maxSize: 1);

            int first = pool.Take();
            int second = pool.Take();
            pool.Return(first);
            Assert.That(pool.Available, Is.EqualTo(1));

            pool.Return(second);
            Assert.That(destroyed, Is.EqualTo(1));
            Assert.That(pool.Available, Is.EqualTo(1));
        }

        [Test]
        public void Return_ItemNotActive_Throws()
        {
            Pool<object> pool = new Pool<object>(() => new object());
            object foreign = new object();
            Assert.Throws<InvalidOperationException>(() => pool.Return(foreign));
        }

        [Test]
        public void Clear_DisposesActiveAndAvailable()
        {
            int destroyed = 0;
            Pool<object> pool = new Pool<object>(() => new object(), _ => destroyed++);

            object a = pool.Take();
            object b = pool.Take();
            pool.Return(a);
            Assert.That(pool.Available, Is.EqualTo(1));

            pool.Clear();
            Assert.That(destroyed, Is.EqualTo(2));
            Assert.That(pool.Available, Is.EqualTo(0));
            Assert.That(pool.Active.Count, Is.EqualTo(0));
        }

        [Test]
        public void InitialSize_PrefillsAvailable()
        {
            int n = 0;
            Pool<int> pool = new Pool<int>(() => ++n, initialSize: 3);
            Assert.That(pool.Available, Is.EqualTo(3));
            Assert.That(pool.Take(), Is.EqualTo(3));
            Assert.That(pool.Available, Is.EqualTo(2));
        }

        private sealed class PoolablePooled : IPoolable
        {
            public int TakenCount { get; private set; }
            public int ReturnedCount { get; private set; }

            public event Action ReturnRequested;

            public void OnTakenFromPool()
            {
                TakenCount++;
            }

            public void OnReturnedToPool()
            {
                ReturnedCount++;
            }

            public void RequestReturn()
            {
                ReturnRequested?.Invoke();
            }
        }
    }
}
