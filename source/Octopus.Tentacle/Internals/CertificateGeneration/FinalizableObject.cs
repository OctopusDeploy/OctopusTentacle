using System;
#if NETFRAMEWORK
using System;
using System.Runtime.InteropServices;

namespace Octopus.Tentacle.Internals.CertificateGeneration
{
    [StructLayout(LayoutKind.Sequential)]
    public abstract class FinalizableObject : IDisposable
    {
        private bool disposed;

        ~FinalizableObject()
        {
            CleanUp(false);
        }

        public void Dispose()
        {
            // note this method does not throw ObjectDisposedException
            if (!disposed)
            {
                CleanUp(true);

                disposed = true;

                GC.SuppressFinalize(this);
            }
        }

        protected abstract void CleanUp(bool viaDispose);

        /// <summary>
        /// Typical check for derived classes
        /// </summary>
        protected void ThrowIfDisposed()
        {
            ThrowIfDisposed(GetType().FullName);
        }

        /// <summary>
        /// Typical check for derived classes
        /// </summary>
        protected void ThrowIfDisposed(string objectName)
        {
            if (disposed)
                throw new ObjectDisposedException(objectName);
        }
    }
}
#endif