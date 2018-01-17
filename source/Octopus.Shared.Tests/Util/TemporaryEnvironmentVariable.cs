using System;

namespace Octopus.Shared.Tests.Util
{
    public class TemporaryEnvironmentVariable : IDisposable
    {
        public string Name { get; }
        public string Value { get; }
        public EnvironmentVariableTarget Target { get; }

        public TemporaryEnvironmentVariable(string name, string value, EnvironmentVariableTarget target = EnvironmentVariableTarget.Process)
        {
            this.Name = name;
            this.Value = value;
            this.Target = target;
            Environment.SetEnvironmentVariable(name, value, target);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(Name, null, Target);
        }
    }
}