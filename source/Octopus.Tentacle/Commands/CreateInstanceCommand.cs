using System;
using Octopus.Shared;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Startup;
using Octopus.Shared.Util;

namespace Octopus.Tentacle.Commands
{
    public class CreateInstanceCommand : AbstractCommand
    {
        readonly IOctopusFileSystem fileSystem;
        readonly IApplicationInstanceSelector instanceSelector;
        string instanceName;
        string config;
        string home;

        public CreateInstanceCommand(IOctopusFileSystem fileSystem, IApplicationInstanceSelector instanceSelector)
        {
            this.fileSystem = fileSystem;
            this.instanceSelector = instanceSelector;
            Options.Add("instance=", "Name of the instance to create", v => instanceName = v);
            Options.Add("config=", "Path to configuration file to create", v => config = v);
            Options.Add("home=", "[Optional] Path to the home directory - defaults to the same directory as the config file", v => home = v);
        }

        protected override void Start()
        {
            if (string.IsNullOrWhiteSpace(config)) throw new ControlledFailureException("No configuration file was specified. Please use the --config parameter to specify a configuration file path.");

            config = fileSystem.GetFullPath(config);

            if (string.IsNullOrWhiteSpace(instanceName))
            {
                instanceSelector.CreateDefaultInstance(config, home);
            }
            else
            {
                instanceSelector.CreateInstance(instanceName, config, home);
            }
        }
    }
}
