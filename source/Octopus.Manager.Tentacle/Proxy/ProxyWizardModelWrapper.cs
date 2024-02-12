using System.Collections.Generic;
using Octopus.Diagnostics;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Tentacle.Util;

namespace Octopus.Manager.Tentacle.Proxy
{
    public class ProxyWizardModelWrapper : ShellViewModel, IScriptableViewModel
    {
        public ProxyWizardModelWrapper(ProxyWizardModel proxyWizardModel) : base(proxyWizardModel.InstanceSelectionModel)
        {
            ProxyWizardModel = proxyWizardModel;
            proxyWizardModel.ToggleService = false;
            InstanceName = proxyWizardModel.InstanceName;
        }

        public string InstanceName { get; }
        public  ProxyWizardModel ProxyWizardModel { get; }
        public PollingProxyWizardModel PollingProxyWizardModel { get; private set; }

        public IEnumerable<CommandLineInvocation> GenerateScript()
        {
            yield return CliBuilder.ForTool(ProxyWizardModel.Executable, "service", InstanceName).Flag("stop").Build();

            foreach (var line in ProxyWizardModel.GenerateScript())
            {
                yield return line;
            }

            if (PollingProxyWizardModel != null)
            {
                foreach (var line in PollingProxyWizardModel.GenerateScript())
                {
                    yield return line;
                }
            }

            yield return CliBuilder.ForTool(ProxyWizardModel.Executable, "service", InstanceName).Flag("start").Build();
        }

        public IEnumerable<CommandLineInvocation> GenerateRollbackScript()
        {
            yield break;
        }

        public void AddPollingModel(PollingProxyWizardModel pollingWizardModel)
        {
            PollingProxyWizardModel = pollingWizardModel;
            PollingProxyWizardModel.ToggleService = false;
        }

        public void ContributeSensitiveValues(ILog log)
        {
            ProxyWizardModel.ContributeSensitiveValues(log);
        }
    }
}