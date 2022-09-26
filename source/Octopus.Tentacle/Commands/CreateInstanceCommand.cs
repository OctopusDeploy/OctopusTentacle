using System;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Commands
{
    public class CreateInstanceCommand : AbstractCommand
    {
        private readonly IApplicationInstanceManager instanceManager;
        private string instanceName = null!;
        private string config = null!;
        private string home = null!;

        public CreateInstanceCommand(IApplicationInstanceManager instanceManager, ILogFileOnlyLogger logFileOnlyLogger) : base(logFileOnlyLogger)
        {
            this.instanceManager = instanceManager;
            Options.Add("instance=", "Name of the instance to create", v => instanceName = v);
            Options.Add("config=", "Path to configuration file to create", v => config = v);
            Options.Add("home=", "[Optional] Path to the home directory - defaults to the same directory as the config file", v => home = v);
        }

        protected override void Start()
        {
            if (string.IsNullOrWhiteSpace(instanceName))
                instanceManager.CreateDefaultInstance(config, home);
            else
                instanceManager.CreateInstance(instanceName, config, home);
        }
    }
}