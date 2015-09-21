namespace Octopus.Shared.Configuration
{
    public interface IAzurePowerShellModuleConfiguration : IModifiableConfiguration
    {
         string AzurePowerShellModule { get; set; }
    }
}