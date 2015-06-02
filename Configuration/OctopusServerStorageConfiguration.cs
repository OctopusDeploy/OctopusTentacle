using System;
using System.Security.Cryptography;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Security.MasterKey;

namespace Octopus.Shared.Configuration
{
    public class OctopusServerStorageConfiguration : IOctopusServerStorageConfiguration
    {
        readonly IKeyValueStore settings;
        readonly ILog log = Log.Octopus();

        public OctopusServerStorageConfiguration(IKeyValueStore settings)
        {
            this.settings = settings;

            if (MasterKey == null)
            {
                log.Info("Generating a new Master Key for this Octopus Server...");
                MasterKey = MasterKeyEncryption.GenerateKey();
                Save();
                log.Info("Master Key saved; use the Octopus Administration tool to back the key up.");
            }
        }

        public string ServerNodeName
        {
            get { return settings.Get("Octopus.Server.NodeName", Environment.MachineName); }
            set { settings.Set("Octopus.Server.NodeName", value); }
        }

        public string ExternalDatabaseConnectionString
        {
            get { return settings.Get("Octopus.Storage.ExternalDatabaseConnectionString"); }
            set { settings.Set("Octopus.Storage.ExternalDatabaseConnectionString", value); }
        }

        public byte[] MasterKey
        {
            get { return settings.Get<byte[]>("Octopus.Storage.MasterKey", protectionScope: DataProtectionScope.LocalMachine); }
            set { settings.Set("Octopus.Storage.MasterKey", value, DataProtectionScope.LocalMachine); }
        }

        public void Save()
        {
            settings.Save();
        }
    }
}