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
        readonly IApplicationInstanceManager instanceManager;
        string instanceName;
        string config;
        string home;

        public CreateInstanceCommand(IOctopusFileSystem fileSystem, IApplicationInstanceManager instanceManager)
        {
            this.fileSystem = fileSystem;
            this.instanceManager = instanceManager;
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
                instanceManager.CreateDefaultInstance(config, home);
            }
            else
            {
                instanceManager.CreateInstance(instanceName, config, home);
            }
        }
    }
}
