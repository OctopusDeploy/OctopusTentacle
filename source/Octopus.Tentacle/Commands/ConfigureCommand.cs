using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Octopus.Client.Model;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Startup;
using Octopus.Shared.Util;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Commands
{
    public class ConfigureCommand : AbstractStandardCommand
    {
        readonly Lazy<ITentacleConfiguration> tentacleConfiguration;
        readonly Lazy<IHomeConfiguration> home;
        readonly ILog log;
        readonly List<string> octopusToAdd = new List<string>();
        readonly List<string> octopusToRemove = new List<string>();
        readonly List<Action> operations = new List<Action>();
        bool resetTrust;

        public ConfigureCommand(
            Lazy<ITentacleConfiguration> tentacleConfiguration,
            Lazy<IHomeConfiguration> home,
            IOctopusFileSystem fileSystem,
            ILog log,
            IApplicationInstanceSelector selector)
            : base(selector)
        {
            this.tentacleConfiguration = tentacleConfiguration;
            this.home = home;
            this.log = log;

            Options.Add("home=|homedir=", "Home directory", v => QueueOperation(delegate
            {
                var fullPath = fileSystem.GetFullPath(v);
                fileSystem.EnsureDirectoryExists(fullPath);
                home.Value.HomeDirectory = fullPath;
                log.Info("Home directory set to: " + fullPath);
                VoteForRestart();
            }));
            Options.Add("app=|appdir=", "Default directory to deploy applications to", v => QueueOperation(delegate
            {
                var fullPath = fileSystem.GetFullPath(v);
                fileSystem.EnsureDirectoryExists(fullPath);
                tentacleConfiguration.Value.ApplicationDirectory = fullPath;
                log.Info("Application directory set to: " + fullPath);
                VoteForRestart();
            }));
            Options.Add("port=", "TCP port on which Tentacle should listen to connections", v => QueueOperation(delegate
            {
                tentacleConfiguration.Value.ServicesPortNumber = int.Parse(v);
                log.Info("Services listen port: " + v);
                VoteForRestart();
            }));
            Options.Add("noListen=", "Suppress listening on a TCP port (intended for polling Tentacles only)", v => QueueOperation(delegate
            {
                var noListen = bool.Parse(v);
                tentacleConfiguration.Value.NoListen = noListen;

                foreach (var server in this.tentacleConfiguration.Value.TrustedOctopusServers.Where(s => octopusToAdd.Contains(s.Thumbprint)))
                    server.CommunicationStyle = noListen ? CommunicationStyle.TentacleActive : CommunicationStyle.TentaclePassive;

                log.Info(tentacleConfiguration.Value.NoListen ? "Tentacle will not listen on a port" : "Tentacle will listen on a TCP port");
                VoteForRestart();
            }));
            Options.Add("listenIpAddress=", "IP address on which Tentacle should listen. Default: any", v => QueueOperation(delegate
            {
                if (string.Equals("any", v, StringComparison.OrdinalIgnoreCase))
                {
                    tentacleConfiguration.Value.ListenIpAddress = null;
                    log.Info("Listen on any IP address");
                }
                else
                {
                    var parsed = IPAddress.Parse(v);
                    tentacleConfiguration.Value.ListenIpAddress = v;
                    log.Info("Listen on IP address: " + parsed);
                }
                VoteForRestart();
            }));
            Options.Add("trust=", "The thumbprint of the Octopus Server to trust", v => octopusToAdd.Add(v));
            Options.Add("remove-trust=", "The thumbprint of the Octopus Server to remove from the trusted list", v => octopusToRemove.Add(v));
            Options.Add("reset-trust", "Removes all trusted Octopus Servers", v => resetTrust = true);
        }

        protected override void Start()
        {
            base.Start();
            if (resetTrust)
            {
                log.Info("Removing all trusted Octopus Servers...");
                tentacleConfiguration.Value.ResetTrustedOctopusServers();
                VoteForRestart();
            }

            if (octopusToRemove.Count > 0)
            {
                log.Info($"Removing {octopusToRemove.Count} trusted Octopus Servers");
                foreach (var toRemove in octopusToRemove)
                {
                    tentacleConfiguration.Value.RemoveTrustedOctopusServersWithThumbprint(toRemove);
                    VoteForRestart();
                }
            }

            if (octopusToAdd.Count > 0)
            {
                log.Info($"Adding {octopusToAdd.Count} trusted Octopus Servers");

                foreach (var toAdd in octopusToAdd)
                {
                    var config = new OctopusServerConfiguration(toAdd) { CommunicationStyle = CommunicationStyle.TentaclePassive };
                    tentacleConfiguration.Value.AddOrUpdateTrustedOctopusServer(config);
                    VoteForRestart();
                }
            }

            foreach (var operation in operations) operation();
        }

        void QueueOperation(Action action)
        {
            operations.Add(action);
        }
    }
}