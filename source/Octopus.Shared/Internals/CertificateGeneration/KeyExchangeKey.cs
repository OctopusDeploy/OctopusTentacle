﻿#if NETFRAMEWORK
using System;

namespace Octopus.Shared.Internals.CertificateGeneration
{
    public class KeyExchangeKey : CryptKey
    {
        internal KeyExchangeKey(CryptContext ctx, IntPtr handle) : base(ctx, handle)
        {
        }

        public virtual KeyType Type => KeyType.Exchange;
    }
}
#endif