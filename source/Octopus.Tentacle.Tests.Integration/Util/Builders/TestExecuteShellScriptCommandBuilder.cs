using System;
using Octopus.Tentacle.Client.Scripts.Models.Builders;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders
{
    public class TestExecuteShellScriptCommandBuilder : ExecuteShellScriptCommandBuilder
    {
        public TestExecuteShellScriptCommandBuilder()
            : base(Guid.NewGuid().ToString(), ScriptIsolationLevel.NoIsolation)
        {
            SetDurationStartScriptCanWaitForScriptToFinish(null);
        }
    }
}