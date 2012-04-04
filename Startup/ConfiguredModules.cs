using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Autofac;

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
                    // Don't throw - it's possible they have a custom setting in machine.config from a third party that happens to start
                    // with the name of one of our modules. Crazier things have happened.
                    continue;
                
                property.SetValue(module, Convert.ChangeType(value, property.PropertyType), null);
            }

            foreach (var module in modules)
            {
                builder.RegisterModule(module);
            }
        }
    }
}