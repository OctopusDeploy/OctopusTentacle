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
        static readonly string TextFormat = "text";
        static readonly string JsonFormat = "json";
        static readonly string[] SupportedFormats = { TextFormat, JsonFormat };

        string format = TextFormat;

        public VersionCommand(ILogFileOnlyLogger logFileOnlyLogger) : base(logFileOnlyLogger)
        {
            Options.Add("format=", $"The format of the output ({string.Join(",", SupportedFormats)}). Defaults to {format}.", v => format = v);
        }

        protected override void Start()
        {
            if (!Enumerable.Contains(SupportedFormats, format, StringComparer.OrdinalIgnoreCase))
                throw new ControlledFailureException($"The format '{format}' is not supported. Try {string.Join(" or ", SupportedFormats)}.");

            if (string.Equals(format, TextFormat, StringComparison.OrdinalIgnoreCase))
            {
                Console.Write(OctopusTentacle.InformationalVersion);
            }
            else if (string.Equals(format, JsonFormat, StringComparison.OrdinalIgnoreCase))
            {
                Console.Write(JsonConvert.SerializeObject(new
                {
                    InformationalVersion = OctopusTentacle.InformationalVersion,
                    MajorMinorPatch = OctopusTentacle.SemanticVersionInfo.MajorMinorPatch,
                    NuGetVersion = OctopusTentacle.SemanticVersionInfo.NuGetVersion,
                    SourceBranchName = OctopusTentacle.SemanticVersionInfo.BranchName,
                    IsPrerelease = OctopusTentacle.Version.IsPrerelease
                }, Formatting.Indented));
            }
        }
    }
}