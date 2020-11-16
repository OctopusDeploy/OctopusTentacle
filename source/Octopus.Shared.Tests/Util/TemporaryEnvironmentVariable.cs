using System;

namespace Octopus.Shared.Tests.Util
{
    public class TemporaryEnvironmentVariable : IDisposable
    {
        public TemporaryEnvironmentVariable(string name, string value, EnvironmentVariableTarget target = EnvironmentVariableTarget.Process)
        {
            Name = name;
            Value = value;
            Target = target;
            Environment.SetEnvironmentVariable(name, value, target);
        }

        public string Name { get; }
        public string Value { get; }
        public EnvironmentVariableTarget Target { get; }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(Name, null, Target);
        }
    }
}