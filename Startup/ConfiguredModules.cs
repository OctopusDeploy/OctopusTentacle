using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Autofac;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Startup
{
    public class ConfiguredModules : Module
    {
        readonly IList<Module> modules;

        public ConfiguredModules(IList<Module> modules)
        {
            this.modules = modules;
        }

        protected override void Load(ContainerBuilder builder)
        {
            var settings = ConfigurationManager.AppSettings;
            var keys = settings.AllKeys;

            var log = Logger.Default;
            log.Debug("The following appSettings are defined:");
            foreach (var key in keys)
            {
                log.Debug("- " + key);
            }

            foreach (var setting in keys)
            {
                if (string.IsNullOrEmpty(setting))
                    continue;

                var parts = setting.Split('.');
                if (parts.Length != 2)
                    continue;

                var moduleName = parts[0];
                var propertyName = parts[1];
                var value = settings[setting];

                var module = modules.FirstOrDefault(x => x.GetType().Name == moduleName + "Module");
                if (module == null)
                    continue;

                var property = module.GetType().GetProperty(propertyName);
                if (property == null)
                {
                    throw new ConfigurationException(string.Format("Invalid configuration key: {0}", setting));
                }
                
                property.SetValue(module, Convert.ChangeType(value, property.PropertyType), null);
            }

            foreach (var module in modules)
            {
                builder.RegisterModule(module);
            }
        }
    }
}