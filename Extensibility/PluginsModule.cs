using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autofac;
using Octopus.Server.Extensibility.Extensions;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;
using Octopus.Diagnostics;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Util;
using Module = Autofac.Module;

namespace Octopus.Shared.Extensibility
{
    public class PluginsModule : Module
    {
        readonly bool suppressInfoLogging;
        readonly ILog log = Log.Octopus();
        readonly ExtensionInfoProvider provider;

        public PluginsModule(ExtensionInfoProvider provider, bool suppressInfoLogging = false)
        {
            this.provider = provider;
            this.suppressInfoLogging = suppressInfoLogging;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterGeneric(typeof(WhenEnabledActionInvoker<,>)).InstancePerDependency();

            var extensions = LoadCustomExtensions(builder);
            LoadBuiltInExtensions(builder, extensions);
        }

        HashSet<string> LoadCustomExtensions(ContainerBuilder builder)
        {
            // load extensions from AppData/Octopus/CustomExtensions
            return LoadExtensions(builder, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Octopus\CustomExtensions"), new HashSet<string>(), true);
        }

        void LoadBuiltInExtensions(ContainerBuilder builder, HashSet<string> alreadyLoadedExtensions)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BuiltInExtensions");
            if (!Directory.Exists(path))
            {
                log.Warn($"Plugins directory does not exist: {path}");
                return;
            }

            if (!suppressInfoLogging)
                log.Info($"Loading plugins from: {path}");

            LoadExtensions(builder, path, alreadyLoadedExtensions, false);
        }

        HashSet<string> LoadExtensions(ContainerBuilder builder, string path, HashSet<string> loadedExtensions, bool isLoadingCustomExtensions)
        {
            if (!Directory.Exists(path))
                return loadedExtensions;

            foreach (var file in Directory.EnumerateFiles(path, "*.dll").Where(f => !loadedExtensions.Contains(Path.GetFileName(f))))
            {
                var assembly = Assembly.LoadFrom(file);
                var containedExtensions = false;

                var extensionTypes = assembly.ExportedTypes.Where(t => t.IsAssignableTo<IOctopusExtension>());
                foreach (var extensionType in extensionTypes)
                {
                    var metadataAttribute = extensionType.GetCustomAttribute(typeof(OctopusPluginAttribute)) as IOctopusExtensionMetadata;
                    var friendlyName = metadataAttribute == null ? extensionType.Name : metadataAttribute.FriendlyName;
                    var author = metadataAttribute == null ? string.Empty : metadataAttribute.Author;
                    var customString = isLoadingCustomExtensions ? "Custom" : "BuiltIn";
                    var version = assembly.GetFileVersion();

                    if (!suppressInfoLogging)
                        log.Info($"Loading {customString} extension: {friendlyName} ({version})");

                    var extensionInstance = (IOctopusExtension)Activator.CreateInstance(extensionType);

                    extensionInstance.Load(builder);

                    if (!suppressInfoLogging)
                    {
                        provider.AddExtensionData(new ExtensionInfo(friendlyName, Path.GetFileName(file), author, version, isLoadingCustomExtensions));
                    }

                    containedExtensions = true;
                }

                if (containedExtensions)
                {
                    loadedExtensions.Add(Path.GetFileName(file));
                }
            }

            return loadedExtensions;
        }
    }
}