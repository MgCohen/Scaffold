#nullable enable
using System;

namespace Scaffold.States
{
    public sealed class CallbackDisposable : IDisposable
    {
        public CallbackDisposable(Action dispose)
        {
            this.dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
        }

        private Action? dispose;

        public void Dispose()
        {
            dispose?.Invoke();
            dispose = null;
        }
    }
}
