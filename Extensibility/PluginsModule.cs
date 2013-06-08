using System;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using Autofac;
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
                log.InfoFormat("Plugins directory does not exist: {0}", path);
                return;
            }
            
            log.DebugFormat("Loading plugins from: {0}", path);
            
            var catalog = new DirectoryCatalog(path);

            var container = new CompositionContainer(catalog);
            var plugins = container.GetExports<IOctopusExtension, IOctopusExtensionMetadata>().Distinct();
            foreach (var item in plugins)
            {
                log.InfoFormat("Loading external plugin: {0}", item.Metadata.FriendlyName);
                item.Value.Load(builder);
            }
        }
    }
}