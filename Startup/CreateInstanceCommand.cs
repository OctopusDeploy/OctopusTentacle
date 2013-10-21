using System;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Util;
using Octopus.Shared.Configuration;

namespace Octopus.Shared.Startup
{
    public class CreateInstanceCommand : AbstractCommand
    {
        readonly IOctopusFileSystem fileSystem;
        readonly IApplicationInstanceSelector instanceSelector;
        string instanceName;
        string config;

        public CreateInstanceCommand(IOctopusFileSystem fileSystem, IApplicationInstanceSelector instanceSelector, ILog log)
        {
            this.fileSystem = fileSystem;
            this.instanceSelector = instanceSelector;
            Options.Add("instance=", "Name of the instance to create", v => instanceName = v);
            Options.Add("config=", "Path to configuration file to create", v => config = v);
        }

        protected override void Start()
        {
            if (string.IsNullOrWhiteSpace(config)) throw new ArgumentException("No configuration file was specified. Please use the --config parameter to specify a configuration file path.");

            config = fileSystem.GetFullPath(config);

            if (string.IsNullOrWhiteSpace(instanceName))
            {
                instanceSelector.CreateDefaultInstance(config);
            }
            else
            {
                instanceSelector.CreateInstance(instanceName, config);
            }
        }
    }
}