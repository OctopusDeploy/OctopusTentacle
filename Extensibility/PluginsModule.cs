using System;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using Autofac;
using Octopus.Server.Extensibility.Diagnostics;
using Octopus.Shared.Diagnostics;

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

            var catalog = new DirectoryCatalog(path);

            var container = new CompositionContainer(catalog);
            var plugins = container.GetExports<IOctopusExtension, IOctopusExtensionMetadata>().Distinct();
            foreach (var item in plugins)
            {
                log.Info($"Loading external plugin: {item.Metadata.FriendlyName}");
                item.Value.Load(builder);
            }
        }
    }
}