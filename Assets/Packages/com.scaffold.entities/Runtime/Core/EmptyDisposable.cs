using System;

namespace Scaffold.Entities
{
    internal sealed class EmptyDisposable : IDisposable
    {
        private EmptyDisposable()
        {
        }

        internal static readonly EmptyDisposable Instance = new EmptyDisposable();

        public void Dispose()
        {
        }
    }
}
