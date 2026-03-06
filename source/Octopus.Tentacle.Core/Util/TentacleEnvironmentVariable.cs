using System;

namespace Octopus.Tentacle.Core.Util
{
    public class TentacleEnvironmentVariable
    {
        public string Key { get; }
        public string? Value { get; }

        public TentacleEnvironmentVariable(string key, string? value)
        {
            Key = key;
            Value = value;
        }
    }
}