using System;

namespace Octopus.Platform.Model.Endpoints
{
    public interface IEndpointWithAccount
    {
        string AccountId { get; }
    }
}