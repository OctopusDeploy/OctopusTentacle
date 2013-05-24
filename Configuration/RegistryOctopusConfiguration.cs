using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Octopus.Shared.Configuration
{
    public class RegistryOctopusConfiguration : IOctopusConfiguration
    {
        readonly IKeyValueStore settings;

        public RegistryOctopusConfiguration(IKeyValueStore settings)
        {
            this.settings = settings;
        }

        public bool BackupsEnabledByDefault
        {
            get { return settings.Get("Octopus.Storage.BackupsEnabledByDefault", true); }
            set { settings.Set("Octopus.Storage.BackupsEnabledByDefault", value); }
        }

        public string EmbeddedDatabaseStoragePath
        {
            get { return settings.Get("Octopus.Storage.Path"); }
            set { settings.Set("Octopus.Storage.Path", value); }
        }

        public bool AllowCheckingForUpgrades
        {
            get { return settings.Get("Octopus.Upgrades.AllowChecking", true); }
            set { settings.Set("Octopus.Upgrades.AllowChecking", value); }
        }

        public bool IncludeUsageStatisticsWhenCheckingForUpgrades
        {
            get { return settings.Get("Octopus.Upgrades.IncludeStatistics", true); }
            set { settings.Set("Octopus.Upgrades.IncludeStatistics", value); }
        }

        public string CacheDirectory
        {
            get
            {
                var path = EmbeddedDatabaseStoragePath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                }

                path = Path.Combine(path, "PackageCache");

                return path;
            }
        }

        public string PackagesIndexDirectory
        {
            get
            {
                var path = EmbeddedDatabaseStoragePath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                }

                path = Path.Combine(path, "PackageIndex");

                return path;
            }
        }

        public int RavenPort
        {
            get { return settings.Get("Octopus.Raven.Port", 10930); }
            set { settings.Set("Octopus.Raven.Port", value); }
        }

        public string RavenHostName
        {
            get { return settings.Get("Octopus.Raven.HostName", "localhost"); }
            set { settings.Set("Octopus.Raven.HostName", value); }
        }
        
        public string RavenConnectionString { get { return string.Format("Url = http://{0}:{1}/", RavenHostName, RavenPort); } }

        public bool SelfHostWebPortal
        {
            get { return settings.Get("Octopus.Portal.Enabled", true); }
            set { settings.Set("Octopus.Portal.Enabled", value); }
        }
        
        public int SelfHostWebPortalPort
        {
            get { return settings.Get("Octopus.Portal.Port", 8050); }
            set { settings.Set("Octopus.Portal.Port", value); }
        }

        public AuthenticationMode AuthenticationMode
        {
            get { return settings.Get("Octopus.Web.AuthenticationMode", AuthenticationMode.UsernamePassword); }
            set { settings.Set("Octopus.Web.AuthenticationMode", value); }
        }

        public string PublicWebPortalAddress
        {
            get { return settings.Get("Octopus.Web.PublicWebPortalAddress", string.Empty); }
            set { settings.Set("Octopus.Web.PublicWebPortalAddress", value); }
        }

        public string LocalWebPortalAddress
        {
            get { return settings.Get("Octopus.Web.LocalWebPortalAddress", string.Empty); }
            set { settings.Set("Octopus.Web.LocalWebPortalAddress", value); }
        }

        public bool LocalWebPortalAddressAutoConfigure
        {
            get { return settings.Get("Octopus.Web.LocalWebPortalAddressAutoConfigure", true); }
            set { settings.Set("Octopus.Web.LocalWebPortalAddressAutoConfigure", value); }
        }

        public string IntegratedFeedApiKey
        {
            get
            {
                EnsureIntegratedFeedApiKey();
                var key = settings.Get("Octopus.NuGet.IntegratedApiKey", "");
                return key;
            }
        }

        public void EnsureIntegratedFeedApiKey()
        {
            var key = settings.Get("Octopus.NuGet.IntegratedApiKey", "");
            if (string.IsNullOrWhiteSpace(key))
            {
                key = GenerateApiKey();
                settings.Set("Octopus.NuGet.IntegratedApiKey", key);
            }
        }

        string GenerateApiKey()
        {
            var key = Guid.NewGuid().ToString();
            var hash = new SHA1Managed().ComputeHash(Encoding.Default.GetBytes(key));
            return new string(Convert.ToBase64String(hash).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        }
    }
}