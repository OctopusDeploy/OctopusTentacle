using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            Console.WriteLine(GetOutput(format, instances));
        }

        public string GetOutput(string outputFormat, IList<ApplicationInstanceRecord> instances)
        {
            var result = new StringBuilder();
            if (string.Equals(outputFormat, "json", StringComparison.InvariantCultureIgnoreCase))
            {
                var json = JsonConvert.SerializeObject(instances.Select(x => new {x.InstanceName, x.ConfigurationFilePath}), Formatting.Indented);
                result.AppendLine(json);
            }
            else
            {
                if (instances.Any())
                {
                    foreach (var instance in instances)
                    {
                        result.AppendLine($"Instance '{instance.InstanceName}' uses configuration '{instance.ConfigurationFilePath}'.");
                    }
                }
                else
                {
                    result.AppendLine("No instances installed");
                }
            }
            return result.ToString();
        }
    }
}
