using System.Collections.Generic;
using Octopus.Diagnostics;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Tentacle.Util;

namespace Octopus.Manager.Tentacle.Proxy
{
    public class ProxyWizardModelWrapper : ViewModel, IScriptableViewModel
    {
        readonly ProxyWizardModel proxyWizardModel;
        ProxyWizardModel pollingProxyWizardModel;

        public ProxyWizardModelWrapper(ProxyWizardModel proxyWizardModel)
        {
            this.proxyWizardModel = proxyWizardModel;
            proxyWizardModel.ToggleService = false;
            InstanceName = proxyWizardModel.InstanceName;
        }

        public string InstanceName { get; }

        public IEnumerable<CommandLineInvocation> GenerateScript()
        {
            yield return CliBuilder.ForTool(proxyWizardModel.Executable, "service", InstanceName).Flag("stop").Build();

            foreach (var line in proxyWizardModel.GenerateScript())
            {
                yield return line;
            }

            if (pollingProxyWizardModel != null)
            {
                foreach (var line in pollingProxyWizardModel.GenerateScript())
                {
                    yield return line;
                }
            }

            yield return CliBuilder.ForTool(proxyWizardModel.Executable, "service", InstanceName).Flag("start").Build();
        }

        public IEnumerable<CommandLineInvocation> GenerateRollbackScript()
        {
            yield break;
        }

        public void AddPollingModel(ProxyWizardModel pollingWizardModel)
        {
            pollingProxyWizardModel = pollingWizardModel;
            pollingProxyWizardModel.ToggleService = false;
        }

        public void ContributeSensitiveValues(ILog log)
        {
            proxyWizardModel.ContributeSensitiveValues(log);
        }
    }
}