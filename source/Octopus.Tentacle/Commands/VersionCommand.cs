using System;
using System.Linq;
using Newtonsoft.Json;
using Octopus.Shared;
using Octopus.Shared.Startup;
using Octopus.Tentacle.Properties;

namespace Octopus.Tentacle.Commands
{
    public class VersionCommand : AbstractCommand
    {
        private static readonly string TextFormat = "text";
        private static readonly string JsonFormat = "json";
        private static readonly string[] SupportedFormats = { TextFormat, JsonFormat };

        private string format = TextFormat;

        public VersionCommand(ILogFileOnlyLogger logFileOnlyLogger) : base(logFileOnlyLogger)
        {
            Options.Add("format=", $"The format of the output ({string.Join(",", SupportedFormats)}). Defaults to {format}.", v => format = v);
        }

        protected override void Start()
        {
            if (!SupportedFormats.Contains(format, StringComparer.OrdinalIgnoreCase))
                throw new ControlledFailureException($"The format '{format}' is not supported. Try {string.Join(" or ", SupportedFormats)}.");

            if (string.Equals(format, TextFormat, StringComparison.OrdinalIgnoreCase))
                Console.Write(OctopusTentacle.InformationalVersion);
            else if (string.Equals(format, JsonFormat, StringComparison.OrdinalIgnoreCase))
                Console.Write(JsonConvert.SerializeObject(new
                {
                    OctopusTentacle.InformationalVersion,
                    OctopusTentacle.SemanticVersionInfo.MajorMinorPatch,
                    OctopusTentacle.SemanticVersionInfo.NuGetVersion,
                    SourceBranchName = OctopusTentacle.SemanticVersionInfo.BranchName,
                    OctopusTentacle.Version.IsPrerelease
                }, Formatting.Indented));
        }
    }
}