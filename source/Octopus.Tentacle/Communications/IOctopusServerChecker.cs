using System;
using System.Net;

namespace Octopus.Tentacle.Communications
{
    public interface IOctopusServerChecker
    {
        OctopusServerCommunicationsCheckResult CheckServerCommunicationsIsOpen(Uri serverAddress, IWebProxy? proxyOverride);
    }
}
