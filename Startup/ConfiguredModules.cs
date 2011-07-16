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
                var parts = setting.Split('.');
                var moduleName = parts[0];
                var propertyName = parts[1];
                var value = settings[setting];

                var module = modules.First(x => x.GetType().Name == moduleName + "Module");
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