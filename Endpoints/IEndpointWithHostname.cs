using System;

namespace Octopus.Shared.Endpoints
{
    public interface IEndpointWithHostname
    {
        string Host { get; }
    }
}