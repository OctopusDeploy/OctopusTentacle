using System;
using System.IO;
using System.Text;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Security;
using Octopus.Shared.Security.Certificates;
using Octopus.Shared.Startup;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Commands
{
    public class NewCertificateCommand : AbstractStandardCommand
    {
        readonly Lazy<ITentacleConfiguration> configuration;
        readonly ILog log;
        readonly Lazy<ICertificateGenerator> generator;
        bool preserve;
        string exportFile;

        public NewCertificateCommand(
            Lazy<ITentacleConfiguration> configuration,
            ILog log,
            IApplicationInstanceSelector selector,
            Lazy<ICertificateGenerator> generator) : base(selector)
        {
            this.configuration = configuration;
            this.log = log;
            this.generator = generator;

            Options.Add("b|if-blank", "Generates a new certificate only if there is none", v => preserve = true);
            Options.Add("e|export-file=", "Exports a new certificate to the specified file as unprotected base64 text, but does not save it to the Tentacle configuration; for use with the import-certificate command", v => exportFile = v);
        }

        protected override void Start()
        {

            if (preserve && !string.IsNullOrWhiteSpace(exportFile))
                throw new ArgumentException("Invalid command: --if-blank and --export-file cannot be specified together");

            if (!string.IsNullOrWhiteSpace(exportFile))
            {
                var cert = generator.Value.GenerateNew(CertificateExpectations.TentacleCertificateFullName);
                var base64 = Convert.ToBase64String(CertificateEncoder.Export(cert));
                File.WriteAllText(exportFile, base64, Encoding.UTF8);
                log.Info($"A new certificate has been generated and written to {exportFile}. Thumbprint:");
                log.Info(cert.Thumbprint);
            }
            else
            {
                if (preserve && configuration.Value.TentacleCertificate != null)
                {
                    log.Info("A certificate already exists, no changes will be applied.");
                    return;
                }

                log.Verbose("Generating and installing a new cetificate...");
                var certificate = configuration.Value.GenerateNewCertificate();
                log.Info("A new certificate has been generated and installed. Thumbprint:");
                log.Info(certificate.Thumbprint);
            }
        }
    }
}