using System;
using System.Net;

namespace Octopus.Shared.Configuration
{
    public interface IProxyInitializer
    {
        void InitializeProxy();
        IWebProxy GetProxy();
    }
}