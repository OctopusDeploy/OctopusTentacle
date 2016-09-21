using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autofac;
using Octopus.Server.Extensibility.Extensions;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Octopus.Shared.Diagnostics;
using Module = Autofac.Module;

namespace Octopus.Shared.Extensibility
{
    public class PluginsModule : Module
    {
        readonly ILog log = Log.Octopus();

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            var provider = new ExtensionInfoProvider();
            builder.Register(c => provider).As<IExtensionInfoProvider>().SingleInstance();

            builder.RegisterGeneric(typeof(WhenEnabledActionInvoker<,>)).InstancePerDependency();

            var extensions = LoadCustomExtensions(builder, provider);
            LoadBuiltInExtensions(builder, extensions, provider);
        }

        HashSet<string> LoadCustomExtensions(ContainerBuilder builder, ExtensionInfoProvider provider)
        {
            // load extensions from AppData/Octopus/CustomExtensions
            return LoadExtensions(builder, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Octopus\CustomExtensions"), new HashSet<string>(), provider, true);
        }

        void LoadBuiltInExtensions(ContainerBuilder builder, HashSet<string> alreadyLoadedExtensions, ExtensionInfoProvider provider)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BuiltInExtensions");
            if (!Directory.Exists(path))
            {
                log.Verbose($"Plugins directory does not exist: {path}");
                return;
            }

            log.Verbose($"Loading plugins from: {path}");

            LoadExtensions(builder, path, alreadyLoadedExtensions, provider, false);
        }

        HashSet<string> LoadExtensions(ContainerBuilder builder, string path, HashSet<string> loadedExtensions, ExtensionInfoProvider provider, bool isLoadingCustomExtensions)
        {
            if (!Directory.Exists(path))
                return loadedExtensions;

            foreach (var file in Directory.EnumerateFiles(path, "*.dll").Where(f => !loadedExtensions.Contains(f)))
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
                    provider.AddExtensionData(new ExtensionInfo(friendlyName, Path.GetFileName(file), isLoadingCustomExtensions));
                }

                loadedExtensions.Add(file);
            }

            return loadedExtensions;
        }
    }
}