using System;

namespace Octopus.Shared.Contracts
{
    public interface IEchoService
    {
        string Echo(string message);
    }
}