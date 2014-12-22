using System;

namespace Octopus.Shared.Endpoints
{
    public interface IEndpointWithAccount
    {
        string AccountId { get; }
    }
}