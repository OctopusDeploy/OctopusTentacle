using System;
#if NETFRAMEWORK
using System;

namespace Octopus.Tentacle.Internals.CertificateGeneration
{
    public enum KeyType
    {
        Exchange = 1,
        Signature = 2
    }
}
#endif