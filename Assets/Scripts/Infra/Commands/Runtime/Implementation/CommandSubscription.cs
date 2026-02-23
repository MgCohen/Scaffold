using System;

namespace Scaffold.Commands
{
    /// <summary>
    /// Disposable token used to unregister command listeners.
    /// </summary>
    public class CommandSubscription : IDisposable
    {
        private readonly Action onDispose;
        private bool isDisposed;

        public CommandSubscription(Action onDisposeAction)
        {
            onDispose = onDisposeAction;
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;
                var disposeAction = onDispose;
                disposeAction?.Invoke();
            }
        }
    }
}
