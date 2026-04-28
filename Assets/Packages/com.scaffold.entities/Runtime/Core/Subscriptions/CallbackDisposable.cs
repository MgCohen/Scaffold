using System;

namespace Scaffold.Entities
{
    internal sealed class CallbackDisposable : IDisposable
    {
        internal CallbackDisposable(Action onDispose)
        {
            this.onDispose = onDispose;
        }

        private Action onDispose;

        public void Dispose()
        {
            onDispose?.Invoke();
            onDispose = null!;
        }
    }
}
