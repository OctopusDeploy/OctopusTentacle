using System;

namespace Octopus.Tentacle.Core.Services
{
    public interface IServiceAttribute
    {
        Type ContractType { get; }
    }
}