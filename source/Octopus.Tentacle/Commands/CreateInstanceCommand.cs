using System;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Commands
{
    public class CreateInstanceCommand : AbstractCommand
    {
        readonly IApplicationInstanceManager instanceManager;
        readonly Lazy<ITentacleConfigurationUpgrade> configurationUpgrade;
        string instanceName = null!;
        string config = null!;
        string home = null!;

        public CreateInstanceCommand(IApplicationInstanceManager instanceManager, ILogFileOnlyLogger logFileOnlyLogger, Lazy<ITentacleConfigurationUpgrade> configurationUpgrade) : base(logFileOnlyLogger)
        {
            this.instanceManager = instanceManager;
            this.configurationUpgrade = configurationUpgrade;
            Options.Add("instance=", "Name of the instance to create", v => instanceName = v);
            Options.Add("config=", "Path to configuration file to create", v => config = v);
            Options.Add("home=", "[Optional] Path to the home directory - defaults to the same directory as the config file", v => home = v);
        }
        
        protected override void Start()
        {
            if (string.IsNullOrWhiteSpace(instanceName))
            {
                instanceManager.CreateDefaultInstance(config, home);
            }
            else
            {
                instanceManager.CreateInstance(instanceName, config, home);
            }

            // The key already exists; this gives it to the owner of a configuration file that was already there.
            configurationUpgrade.Value.PrepareKey();
        }
    }
}
