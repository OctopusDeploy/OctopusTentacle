using System;
using System.Linq;
using Newtonsoft.Json;
using Octopus.Shared.Configuration;
using Octopus.Shared.Startup;

namespace Octopus.Tentacle.Commands
{
    public class ListInstancesCommand : AbstractCommand
    {
        readonly IApplicationInstanceStore instanceStore;
        string format = "text";

        public ListInstancesCommand(IApplicationInstanceStore instanceStore)
        {
            this.instanceStore = instanceStore;
            Options.Add("format=", "The format of the export. Can be 'text' or 'json'. Defaults to 'json'.", v => format = v);
        }

        protected override void Start()
        {
            var instances = instanceStore.ListInstances(ApplicationName.Tentacle);
            if (string.Equals(format, "json", StringComparison.InvariantCultureIgnoreCase))
            {
                var json = JsonConvert.SerializeObject(instances.Select(x => new { x.InstanceName, x.ConfigurationFilePath }), Formatting.Indented);
                Console.WriteLine(json);
            }
            else
            {
                if (instances.Any())
                {
                    foreach (var instance in instances)
                    {
                        Console.WriteLine($"Instance '{instance.InstanceName}' uses configuration '{instance.ConfigurationFilePath}'.");
                    }
                }
                else
                {
                    Console.WriteLine("No instances installed");
                }
            }
        }
    }
}
