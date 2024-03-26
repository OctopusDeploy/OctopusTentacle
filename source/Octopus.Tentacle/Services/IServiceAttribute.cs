using System;

namespace Octopus.Tentacle.Services
{
    public interface IServiceAttribute
    {
        Type ContractType { get; }
    }
}