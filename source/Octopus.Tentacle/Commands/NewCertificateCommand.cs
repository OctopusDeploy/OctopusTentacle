using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Octopus.Diagnostics;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Security;
using Octopus.Tentacle.Security.Certificates;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Commands
{
    public class NewCertificateCommand : AbstractStandardCommand
    {
        private readonly Lazy<IWritableTentacleConfiguration> configuration;
        private readonly ISystemLog log;
        private readonly Lazy<ICertificateGenerator> generator;
        private bool preserve;
        private string exportFile = null!;
        private string exportPfx = null!;
        private string password = null!;

        public NewCertificateCommand(
            Lazy<IWritableTentacleConfiguration> configuration,
            ISystemLog log,
            IApplicationInstanceSelector selector,
            Lazy<ICertificateGenerator> generator,
            ILogFileOnlyLogger logFileOnlyLogger) : base(selector, log, logFileOnlyLogger)
        {
            this.configuration = configuration;
            this.log = log;
            this.generator = generator;

            Options.Add("b|if-blank", "Generates a new certificate only if there is none", v => preserve = true);
            Options.Add("e|export-file=", "DEPRECATED: Exports a new certificate to the specified file as unprotected base64 text, but does not save it to the Tentacle configuration; for use with the import-certificate command", v => exportFile = v);
            Options.Add("export-pfx=", "Exports the new certificate to the specified file as a password protected pfx, but does not save it to the Tentacle configuration; for use with the import-certificate command", v => exportPfx = v);
            Options.Add("pfx-password=", "The password to use for the exported pfx file", v => password = v, sensitive: true);
        }

        protected override void Start()
        {
            base.Start();
            if (preserve && !string.IsNullOrWhiteSpace(exportFile))
                throw new ControlledFailureException("Invalid command: --if-blank and --export-file cannot be specified together");
            if (preserve && !string.IsNullOrWhiteSpace(exportPfx))
                throw new ControlledFailureException("Invalid command: --if-blank and --export-pfx cannot be specified together");
            if (!string.IsNullOrWhiteSpace(exportFile) && !string.IsNullOrWhiteSpace(exportPfx))
                throw new ControlledFailureException("Invalid command: --export-file and --export-pfx cannot be specified together");

            if (!string.IsNullOrWhiteSpace(exportFile))
            {
                var cert = generator.Value.GenerateNew(CertificateExpectations.TentacleCertificateFullName);
                var base64 = Convert.ToBase64String(CertificateEncoder.Export(cert));
                File.WriteAllText(exportFile, base64, Encoding.UTF8);
                log.Info($"A new certificate has been generated and written to {exportFile}. Thumbprint:");
                log.Info(cert.Thumbprint);
            }
            else if (!string.IsNullOrWhiteSpace(exportPfx))
            {
                var cert = generator.Value.GenerateNew(CertificateExpectations.TentacleCertificateFullName);
                var pfxBytes = cert.Export(X509ContentType.Pkcs12, password);
                File.WriteAllBytes(exportPfx, pfxBytes);
                log.Info($"A new certificate has been generated and written to {exportPfx}. Thumbprint:");
                log.Info(cert.Thumbprint);
            }
            else
            {
                if (preserve && configuration.Value.TentacleCertificate != null)
                {
                    log.Info("A certificate already exists, no changes will be applied.");
                    return;
                }

                log.Verbose("Generating and installing a new certificate...");
                var certificate = configuration.Value.GenerateNewCertificate();
                VoteForRestart();
                log.Info("A new certificate has been generated and installed. Thumbprint:");
                log.Info(certificate.Thumbprint);
            }
        }
    }
}