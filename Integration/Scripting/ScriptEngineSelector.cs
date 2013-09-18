using System;
using System.IO;
using Octopus.Platform.Deployment.Configuration;
using Octopus.Platform.Deployment.Conventions;
using Octopus.Platform.Variables;
using Octopus.Shared.Integration.Scripting.PowerShell;
using Octopus.Shared.Integration.Scripting.ScriptCS;

namespace Octopus.Shared.Integration.Scripting
{
    public class ScriptEngineSelector : IScriptRunner
    {
        readonly IProxyConfiguration proxyConfiguration;

        public ScriptEngineSelector(IProxyConfiguration proxyConfiguration)
        {
            this.proxyConfiguration = proxyConfiguration;
        }

        public string[] GetSupportedExtensions()
        {
            return new[] { "ps1", "csx" };
        }

        public ScriptExecutionResult Execute(ScriptArguments arguments)
        {
            var runner = SelectEngine(arguments);
            return runner.Execute(arguments);
        }

        IScriptRunner SelectEngine(ScriptArguments arguments)
        {
            if (string.Equals(Path.GetExtension(arguments.ScriptFilePath), ".csx", StringComparison.OrdinalIgnoreCase))
            {
                return new ScriptCSRunner();
            }

            return new FileBasedPowerShellRunner(proxyConfiguration);
        }
    }
}