using System;
using System.Linq;
using Octopus.Configuration;
using Octopus.Diagnostics;

namespace Octopus.Shared.Configuration.Instances
{
    class AggregatedKeyValueStore : IKeyValueStore
    {
        readonly ILog log;
        readonly IKeyValueStore[] configurations;

        public AggregatedKeyValueStore(ILog log, IKeyValueStore[] configurations)
        {
            this.log = log;
            this.configurations = configurations;
        }

        public string? Get(string name, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            return configurations.Select(c => c.Get(name, protectionLevel)).FirstOrDefault(x => x != null);
        }

        public TData Get<TData>(string name, TData defaultValue = default(TData), ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            return configurations.Select(c => c.Get<TData>(name, defaultValue, protectionLevel)).FirstOrDefault(x => x != null);
        }

        public bool Set(string name, string? value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            return Set<string?>(name, value, protectionLevel);
        }

        public bool Set<TData>(string name, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            var valueChanged = false;
            foreach (var configuration in configurations)
            {
                valueChanged |= configuration.Set(name, value, protectionLevel);
            }
            if (!valueChanged)
                log.ErrorFormat("The configuration setting '{0}' cannot be written when no instance name or configuration file is specified", name);
            return valueChanged;
        }

        public bool Remove(string name)
        {
            var valueRemoved = false;
            foreach (var configuration in configurations)
            {
                valueRemoved |= configuration.Remove(name);
            }
            if (!valueRemoved)
                log.ErrorFormat("The configuration setting '{0}' cannot be removed when no instance name or configuration file is specified", name);
            return valueRemoved;
        }

        public bool Save()
        {
            var configSaved = false;
            foreach (var configuration in configurations)
            {
                configSaved |= configuration.Save();
            }
            if (!configSaved)
                log.Error("The configuration cannot be saved when no instance name or configuration file is specified");
            return configSaved;
        }
    }
}