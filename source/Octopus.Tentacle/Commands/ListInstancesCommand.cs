using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Octopus.Shared;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Startup;

namespace Octopus.Tentacle.Commands
{
    public class ListInstancesCommand : AbstractCommand
    {
        static readonly string TextFormat = "text";
        static readonly string JsonFormat = "json";
        static readonly string[] SupportedFormats = { TextFormat, JsonFormat };

        readonly IApplicationInstanceStore instanceStore;
        public string Format { get; set; } = TextFormat;

        public override bool SuppressConsoleLogging => true;

        public ListInstancesCommand(IApplicationInstanceStore instanceStore, ILogFileOnlyLogger logFileOnlyLogger) : base(logFileOnlyLogger)
        {
            this.instanceStore = instanceStore;

            Options.Add("format=", $"The format of the output ({string.Join(",", SupportedFormats)}). Defaults to {Format}.", v => Format = v);
        }

        protected override void Start()
        {
            if (!SupportedFormats.Contains(Format, StringComparer.OrdinalIgnoreCase))
                throw new ControlledFailureException($"The format '{Format}' is not supported. Try {string.Join(" or ", SupportedFormats)}.");

            var instances = instanceStore.ListInstances();
            Console.Write(GetOutput(instances));
        }

        public string GetOutput(IList<ApplicationInstanceRecord> instances)
        {
            var results = new StringBuilder();
            if (string.Equals(Format, JsonFormat, StringComparison.OrdinalIgnoreCase))
            {
                var json = JsonConvert.SerializeObject(instances.Select(x => new { x.InstanceName, x.ConfigurationFilePath }), Formatting.Indented);
                results.Append(json);
            }
            else if (string.Equals(Format, TextFormat, StringComparison.OrdinalIgnoreCase))
            {
                if (instances.Any())
                {
                    foreach (var instance in instances)
                    {
                        results.AppendLine($"Instance '{instance.InstanceName}' uses configuration '{instance.ConfigurationFilePath}'.");
                    }
                }
                else
                {
                    results.Append("No instances installed");
                }
            }

            return results.ToString();
        }
    }
}
