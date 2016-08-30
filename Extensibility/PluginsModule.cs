using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Autofac;
using Octopus.Server.Extensibility;
using Octopus.Server.Extensibility.Diagnostics;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Internals.Options;
using Module = Autofac.Module;

namespace Octopus.Shared.Extensibility
{
    public class PluginsModule : Module
    {
        readonly ILog log = Log.Octopus();

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OctopusPlugins");
            if (!Directory.Exists(path))
            {
                log.Verbose($"Plugins directory does not exist: {path}");
                return;
            }

            log.Verbose($"Loading plugins from: {path}");

            LoadExtensions(builder, path);
        }

        void LoadExtensions(ContainerBuilder builder, string path)
        {
            foreach (var file in Directory.EnumerateFiles(path, "*.dll"))
            {
                var assembly = Assembly.LoadFrom(file);

                var extensionTypes = assembly.ExportedTypes.Where(t => t.IsAssignableTo<IOctopusExtension>());
                foreach (var extensionType in extensionTypes)
                {
                    var metadataAttribute = extensionType.GetCustomAttribute(typeof(OctopusPluginAttribute)) as IOctopusExtensionMetadata;
                    var friendlyName = metadataAttribute == null ? extensionType.Name : metadataAttribute.FriendlyName;
                    log.Verbose($"Loading external plugin: {friendlyName}");

                    var extensionInstance = (IOctopusExtension)Activator.CreateInstance(extensionType);

                    extensionInstance.Load(builder);
                }
            }
        }
    }
}