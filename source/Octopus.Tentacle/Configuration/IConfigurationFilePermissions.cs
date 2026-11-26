using System;

namespace Octopus.Tentacle.Configuration
{
    public interface IConfigurationFilePermissions
    {
        void CreateRestrictedToCurrentUser(string configurationPath, string contents);
        void RestrictToCurrentUser(string configurationPath);
        void RestrictToServiceUser(string configurationPath, string userName);
    }
}
