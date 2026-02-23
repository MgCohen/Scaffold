using System;

namespace Scaffold.NetworkMessages
{
    /// <summary>
    /// Disposable token used to remove command subscriptions.
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
                var callback = onDispose;
                callback?.Invoke();
            }
        }
    }
}
