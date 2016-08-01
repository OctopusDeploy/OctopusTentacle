using Octopus.Server.Extensibility.Configuration;

namespace Octopus.Shared.Configuration
{
    public interface IAzurePowerShellModuleConfiguration : IModifiableConfiguration
    {
         string AzurePowerShellModule { get; set; }
    }
}