using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Tentacle.Util;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard.Views
{
    public class InstallTabViewModel : IScriptableViewModel
    {
        public async Task<bool> GenerateAndExecuteScript()
        {
            return await Task.FromResult(true);
        }

        public void ContributeSensitiveValues(ILog log)
        {
            throw new NotImplementedException();
        }

        public string InstanceName { get; }
        public IEnumerable<CommandLineInvocation> GenerateScript()
        {
            return Enumerable.Empty<CommandLineInvocation>();
        }

        public IEnumerable<CommandLineInvocation> GenerateRollbackScript()
        {
            return Enumerable.Empty<CommandLineInvocation>();
        }
    }
}
