using System;
using System.Net;

namespace Octopus.Tentacle.Communications
{
    public interface IOctopusServerChecker
    {
        string CheckServerCommunicationsIsOpen(Uri serverAddress, IWebProxy proxyOverride);
    }
}