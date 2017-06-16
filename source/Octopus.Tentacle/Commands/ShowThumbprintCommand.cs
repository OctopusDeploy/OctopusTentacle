using System;
using System.IO;
using System.Text;
using NLog;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Startup;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Commands
{
    public class ShowThumbprintCommand : AbstractStandardCommand
    {
        readonly Lazy<ITentacleConfiguration> tentacleConfiguration;
        readonly ILog log;
        string exportFile;
        bool thumbprintOnly;

        public ShowThumbprintCommand(Lazy<ITentacleConfiguration> tentacleConfiguration, ILog log, IApplicationInstanceSelector selector) : base(selector)
        {
            this.tentacleConfiguration = tentacleConfiguration;
            this.log = log;

            Options.Add("e|export-file=", "Exports the Tentacle thumbprint to a file", v => exportFile = v);
            Options.Add("thumbprint-only", "Only print out the thumbprint, with no additional text", s => thumbprintOnly = true);
        }

        protected override void Start()
        {
            var thumbprint = tentacleConfiguration.Value.TentacleCertificate.Thumbprint;
            var message = (thumbprintOnly ? "" : "The thumbprint of this Tentacle is: ") + thumbprint;

            if (IsConsoleLoggingSuppressed())
            {
                Console.WriteLine(message);
            }

            log.Info(message);
            if (!string.IsNullOrWhiteSpace(exportFile))
            {
                File.WriteAllText(exportFile, thumbprint, Encoding.ASCII);
                log.Info($"The thumbprint has been written to {exportFile}.");
            }
        }

        bool IsConsoleLoggingSuppressed()
        {
            var c = LogManager.Configuration;
            // Note: this matches the target name in tentacle.exe.nlog
            var stdoutTarget = c.FindTargetByName("stdout");
            foreach (var rule in c.LoggingRules)
            {
                if (rule.Targets.Contains(stdoutTarget))
                    return false;
            }
            return true;
        }
    }
}