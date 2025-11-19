using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Commands
{
    public class ShowThumbprintCommand : AbstractStandardCommand
    {
        static readonly string TextFormat = "text";
        static readonly string JsonFormat = "json";
        static readonly string[] SupportedFormats = { TextFormat, JsonFormat };

        readonly Lazy<ITentacleConfiguration> tentacleConfiguration;
        readonly ISystemLog log;
        string exportFile = null!;

        public string Format { get; set; } = TextFormat;

        public override bool SuppressConsoleLogging => true;

        public ShowThumbprintCommand(Lazy<ITentacleConfiguration> tentacleConfiguration, ISystemLog log, IApplicationInstanceSelector selector, ILogFileOnlyLogger logFileOnlyLogger) : base(selector, log, logFileOnlyLogger)
        {
            this.tentacleConfiguration = tentacleConfiguration;
            this.log = log;

            Options.Add("e|export-file=", "Exports the Tentacle thumbprint to a file", v => exportFile = v);
            // See https://github.com/OctopusDeploy/OctopusTentacle/issues/23
            Options.Add("thumbprint-only", "DEPRECATED: Only print out the thumbprint, with no additional text. This switch has been deprecated and will be removed in Octopus 4.0 since it is no longer needed.", s => { });
            Options.Add("format=", $"The format of the output ({string.Join(",", SupportedFormats)}). Defaults to {Format}.", v => Format = v);
        }

        protected override void Start()
        {
            base.Start();

            if (!SupportedFormats.Contains(Format, StringComparer.OrdinalIgnoreCase))
                throw new ControlledFailureException($"The format '{Format}' is not supported. Try {string.Join(" or ", SupportedFormats)}.");

            var thumbprint = tentacleConfiguration.Value.TentacleCertificate?.Thumbprint;

            var content = string.Equals(Format, JsonFormat, StringComparison.OrdinalIgnoreCase)
                ? JsonConvert.SerializeObject(new { Thumbprint = thumbprint })
                : thumbprint;

            if (!string.IsNullOrWhiteSpace(exportFile))
            {
                File.WriteAllText(exportFile, content, Encoding.ASCII);
                log.Info($"The thumbprint has been written to {exportFile}.");
            }
            else
            {
                Console.Write(content);
            }
        }
    }
}