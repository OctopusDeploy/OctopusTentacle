using System;

namespace Octopus.Tentacle.Contracts
{
    internal static class TentacleContracts
    {
        public static string AssemblyName => typeof(TentacleContracts).Assembly.FullName ?? throw new InvalidOperationException();
        public static string Namespace => typeof(TentacleContracts).Namespace ?? throw new InvalidOperationException();
    }
}