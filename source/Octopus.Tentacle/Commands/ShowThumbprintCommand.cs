using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Octopus.Diagnostics;
using Octopus.Shared;
using Octopus.Shared.Configuration;
using Octopus.Shared.Startup;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Commands
{
    public class ShowThumbprintCommand : AbstractStandardCommand
    {
        static readonly string TextFormat = "text";
        static readonly string JsonFormat = "json";
        static readonly string[] SupportedFormats = { TextFormat, JsonFormat };

        readonly Lazy<ITentacleConfiguration> tentacleConfiguration;
        readonly ILog log;
        string exportFile;

        public string Format { get; set; } = TextFormat;

        public override bool SuppressConsoleLogging => true;

        public ShowThumbprintCommand(Lazy<ITentacleConfiguration> tentacleConfiguration, ILog log, IApplicationInstanceSelector selector) : base(selector)
        {
            this.tentacleConfiguration = tentacleConfiguration;
            this.log = log;

            Options.Add("e|export-file=", "Exports the Tentacle thumbprint to a file", v => exportFile = v);
            ThumbprintOnlyOption();
            Options.Add("format=", $"The format of the output ({string.Join(",", SupportedFormats)}). Defaults to {Format}.", v => Format = v);
        }

        [ObsoleteEx(Message = "The --thumbprint-only switch has been deprecated since it is no longer needed and will be removed in 4.0", RemoveInVersion = "4.0", TreatAsErrorFromVersion = "4.0")]
        void ThumbprintOnlyOption()
        {
            Options.Add("thumbprint-only", "Only print out the thumbprint, with no additional text. This switch will be removed in a future version since it is no longer needed.", s => { });
        }

        protected override void Start()
        {
            base.Start();

            if (!SupportedFormats.Contains(Format, StringComparer.OrdinalIgnoreCase))
                throw new ControlledFailureException($"The format '{Format}' is not supported. Try {string.Join(" or ", SupportedFormats)}.");

            var thumbprint = tentacleConfiguration.Value.TentacleCertificate.Thumbprint;

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