using System;
using Microsoft.WindowsAzure.ServiceManagement;

namespace Octopus.Shared.Integration.Azure
{
    public interface IAzureClient : IDisposable
    {
        IServiceManagement Service { get; }
    }
}