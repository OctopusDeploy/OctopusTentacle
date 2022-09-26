using System;
#if NETFRAMEWORK
using System;

namespace Octopus.Tentacle.Internals.CertificateGeneration
{
    public abstract class CryptKey : FinalizableObject
    {
        private readonly CryptContext ctx;

        internal CryptKey(CryptContext ctx, IntPtr handle)
        {
            this.ctx = ctx;
            Handle = handle;
        }

        internal IntPtr Handle { get; }

        protected override void CleanUp(bool viaDispose)
        {
            // keys are invalid once CryptContext is closed,
            // so the only time I try to close an individual key is if a user
            // explicitly disposes of the key.
            if (viaDispose)
                ctx.DestroyKey(this);
        }
    }
}
#endif