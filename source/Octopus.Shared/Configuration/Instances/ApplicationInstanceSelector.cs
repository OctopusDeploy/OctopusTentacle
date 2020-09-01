using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Octopus.Shared.Startup;

namespace Octopus.Shared.Configuration.Instances
{
    public class ApplicationInstanceSelector : IApplicationInstanceSelector
    {
        readonly StartUpInstanceRequest startUpInstanceRequest;
        readonly IApplicationInstanceStrategy[] instanceStrategies;
        readonly ILogFileOnlyLogger logFileOnlyLogger;
        readonly object @lock = new object();
        LoadedApplicationInstance? current;

        public ApplicationInstanceSelector(StartUpInstanceRequest startUpInstanceRequest,
            IApplicationInstanceStrategy[] instanceStrategies,
            ILogFileOnlyLogger logFileOnlyLogger)
        {
            this.startUpInstanceRequest = startUpInstanceRequest;
            this.instanceStrategies = instanceStrategies;
            this.logFileOnlyLogger = logFileOnlyLogger;
        }

        public ApplicationName ApplicationName => startUpInstanceRequest.ApplicationName;

        public IList<ApplicationInstanceRecord> ListInstances()
        {
            return instanceStrategies.SelectMany(s => s.ListInstances()).ToList();
        }
        
        public bool TryGetCurrentInstance([NotNullWhen(true)] out LoadedApplicationInstance? instance)
        {
            instance = null;
            try
            {
                instance = GetCurrentInstance();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public LoadedApplicationInstance GetCurrentInstance()
        {
            if (current == null)
            {
                lock (@lock)
                {
                    if (current == null)
                        current = LoadCurrentInstance();
                }
            }
            return current;
        }
        
        LoadedApplicationInstance LoadCurrentInstance()
        {
            var instance = LoadInstance();

            // BEWARE if you try to resolve HomeConfiguration from the container you'll create a loop
            // back to here
            var homeConfig = new HomeConfiguration(startUpInstanceRequest.ApplicationName, instance.Configuration);
            var logInit = new LogInitializer(new LoggingConfiguration(homeConfig), logFileOnlyLogger);
            logInit.Start();

            return instance;
        }

        internal LoadedApplicationInstance LoadInstance()
        {
            if (startUpInstanceRequest is StartUpPersistedInstanceRequest persistedRequest)
            {
                // possible instances where the name matches, or no instance name was specified 
                var possibleNamedInstances = instanceStrategies
                    .OrderBy(s => s.Priority)
                    .Select(s => new
                    {
                        Strategy = s,
                        Instances = s.ListInstances()
                            .Where(i => string.Equals(i.InstanceName, persistedRequest.InstanceName, StringComparison.InvariantCultureIgnoreCase))
                    })
                    .Where(x => x.Instances.Any())
                    .ToArray();
                
                if (possibleNamedInstances.Length == 0)
                    throw new ControlledFailureException($"Instance {persistedRequest.InstanceName} of {persistedRequest.ApplicationName} has not been configured on this machine. Available instances: {AvailableInstances()}.");

                if (possibleNamedInstances.Length == 1 && possibleNamedInstances.First().Instances.Count() == 1)
                {
                    var strategy = possibleNamedInstances.First().Strategy;
                    var applicationInstanceRecord = possibleNamedInstances.First().Instances.First();
                    return strategy.LoadedApplicationInstance(applicationInstanceRecord);
                }
                // to get more than 1, there must have been a match on differing case, try an exact case match
                var exactMatch = possibleNamedInstances.FirstOrDefault(x => x.Instances.Any(i => i.InstanceName == persistedRequest.InstanceName));
                if (exactMatch == null) // null here means all matches were different case
                    throw new ControlledFailureException($"Instance {persistedRequest.InstanceName} of {persistedRequest.ApplicationName} could not be matched to one of the existing instances: {AvailableInstances()}.");
                return exactMatch.Strategy.LoadedApplicationInstance(exactMatch.Instances.First(i => i.InstanceName == persistedRequest.InstanceName));
            }

            // Non-persisted strategies behave the same way, a single default instance if they are in play
            var possibleInstances = instanceStrategies
                .OrderBy(s => s.Priority)
                .Select(s => new
                {
                    Strategy = s,
                    Instances = s.ListInstances().Where(i => i.IsDefaultInstance)
                })
                .Where(x => x.Instances.Any())
                .ToArray();

            // dynamic strategies should only allow 1 default, so if we get any then take the first one by priority order 
            if (possibleInstances.Any())
            {
                var match = possibleInstances.First();
                return match.Strategy.LoadedApplicationInstance(match.Instances.First());
            }
                
            // if there is no instance specified, and there are no default instances, but there is a single named instance then we'll take it
            possibleInstances = instanceStrategies
                .OrderBy(s => s.Priority)
                .Select(s => new
                {
                    Strategy = s,
                    Instances = s.ListInstances().Where(i => !i.IsDefaultInstance)
                })
                .Where(x => x.Instances.Any())
                .ToArray();

            if (possibleInstances.Length == 1 && possibleInstances.First().Instances.Count() == 1)
            {
                var match = possibleInstances.First();
                return match.Strategy.LoadedApplicationInstance(match.Instances.First());
            }

            if (possibleInstances.Any())
                throw new ControlledFailureException($"There is more than one instance of OctopusServer configured on this machine. Please pass --instance=INSTANCENAME when invoking this command to target a specific instance. Available instances: {AvailableInstances()}.");

            throw new ControlledFailureException(
                $"There are no instances of {startUpInstanceRequest.ApplicationName} configured on this machine. Please run the setup wizard, configure an instance using the command-line interface, specify a configuration file, or set the required environment variables.");
        }

        string AvailableInstances()
        {
            return string.Join(", ", instanceStrategies
                    .OrderBy(s => s.Priority)
                    .SelectMany(s => s.ListInstances().Select(x => x.InstanceName)));
        }
    }
}