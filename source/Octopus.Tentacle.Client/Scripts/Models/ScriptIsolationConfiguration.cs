using System;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts.Models
{
    public record ScriptIsolationConfiguration(
        ScriptIsolationLevel IsolationLevel,
        string MutexName,
        TimeSpan MutexTimeout)
    {
        public static readonly TimeSpan NoTimeout= TimeSpan.FromMilliseconds(int.MaxValue);
    }
}