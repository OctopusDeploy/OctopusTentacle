using System;

namespace Octopus.Shared.Security.CertificateGeneration
{
    public class KeyExchangeKey : CryptKey
    {
        internal KeyExchangeKey(CryptContext ctx, IntPtr handle) : base(ctx, handle)
        {
        }

        public override KeyType Type
        {
            get { return KeyType.Exchange; }
        }
    }
}