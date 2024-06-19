using System;
using System.IO;

namespace Octopus.Tentacle.Configuration.Instances
{
    public class ChangeDetectingKeyValueStore : IKeyValueStore, IDisposable
    {
        IKeyValueStore configuration;
        readonly Func<IKeyValueStore?>? loadConfigurationFunction;
        readonly FileSystemWatcher? configurationFileWatcher;

        public ChangeDetectingKeyValueStore(IKeyValueStore initialConfiguration, string? configurationPath, Func<IKeyValueStore?>? loadConfigurationFunction)
        {
            this.configuration = initialConfiguration;
            this.loadConfigurationFunction = loadConfigurationFunction;

            if (loadConfigurationFunction != null && configurationPath != null)
            {
                var configurationFileInfo = new FileInfo(configurationPath);

                if (configurationFileInfo.DirectoryName != null)
                {
                    configurationFileWatcher = new FileSystemWatcher(configurationFileInfo.DirectoryName, configurationFileInfo.Name);
                    configurationFileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                    configurationFileWatcher.EnableRaisingEvents = true;
                    configurationFileWatcher.Changed += ConfigurationChanged;
                }
            }
        }

        public ConfigurationChangedEventHandler? Changed { get; set; }

        public string? Get(string name, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            return configuration.Get(name, protectionLevel);
        }

        public TData? Get<TData>(string name, TData? defaultValue = default, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            return configuration.Get(name, defaultValue, protectionLevel);
        }

        void ConfigurationChanged(object sender, FileSystemEventArgs args)
        {
            lock (configuration)
            {
                try
                {
                    var updatedConfiguration = loadConfigurationFunction!();

                    if (updatedConfiguration != null)
                    {
                        configuration = updatedConfiguration;

                        Changed?.Invoke();
                    }
                }
                catch (IOException)
                {
                    // Tentacle  writes configuration changes non atomically so may still be writing to the file when the change notification is raised
                }
            }
        }

        public void Dispose()
        {
            configurationFileWatcher?.Dispose();
        }
    }
}