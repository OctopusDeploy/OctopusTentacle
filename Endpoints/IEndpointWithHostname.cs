using System;

namespace Octopus.Platform.Model.Endpoints
{
    public interface IEndpointWithHostname
    {
        string Host { get; }
    }
}