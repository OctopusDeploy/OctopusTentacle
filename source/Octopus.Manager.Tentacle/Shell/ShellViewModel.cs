using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Tentacle.Versioning;

namespace Octopus.Manager.Tentacle.Shell
{
    public class ShellViewModel : ViewModel
    {
        public ShellViewModel(InstanceSelectionModel instanceSelectionModel)
        {
            InstanceSelectionModel = instanceSelectionModel;
            VersionNumber = TentacleManager.SemanticVersionInfo.NuGetVersion;
            ShowEAPVersion = TentacleManager.SemanticVersionInfo.IsEarlyAccessProgram();
        }

        public string VersionNumber { get; }

        public bool ShowEAPVersion { get; set; }

        public InstanceSelectionModel InstanceSelectionModel { get; }
    }
}