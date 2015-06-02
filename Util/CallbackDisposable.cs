using System;

namespace Octopus.Shared.Util
{
    public class CallbackDisposable : IDisposable
    {
        readonly Action disposed;

        public CallbackDisposable(Action disposed)
        {
            this.disposed = disposed;
        }

        public void Dispose()
        {
            if (disposed != null) disposed();
        }
    }
}