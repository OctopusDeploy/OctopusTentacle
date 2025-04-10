﻿using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Win32;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Security.Certificates;
using Octopus.Tentacle.Startup;
using CertificateGenerator = Octopus.Tentacle.Certificates.CertificateGenerator;

namespace Octopus.Tentacle.Commands
{
    public class ImportCertificateCommand : AbstractStandardCommand
    {
        readonly Lazy<IWritableTentacleConfiguration> tentacleConfiguration;
        readonly ISystemLog log;
        bool fromRegistry;
        string importFile = null!;
        string importBase64 = null!;
        string importPfxPassword = null!;

        public ImportCertificateCommand(Lazy<IWritableTentacleConfiguration> tentacleConfiguration, ISystemLog log, IApplicationInstanceSelector selector, ILogFileOnlyLogger logFileOnlyLogger)
            : base(selector, log, logFileOnlyLogger)
        {
            this.tentacleConfiguration = tentacleConfiguration;
            this.log = log;

            Options.Add("r|from-registry", "Import the Octopus Tentacle 1.x certificate from the Windows registry", v => fromRegistry = true);
            Options.Add("f|from-file=", "Import a certificate from the specified file generated by the new-certificate command or a Personal Information Exchange (PFX) file", v => importFile = v);
            Options.Add("b|from-base64=", "Import a certificate from a base64 formatted string", v => importBase64 = v);
            Options.Add("pw|pfx-password=", "Personal Information Exchange (PFX) private key password", v => importPfxPassword = v, sensitive: true);
        }

        protected override void Start()
        {
            base.Start();
            
            var specifiedCertOptionsCount = new[] {fromRegistry, !string.IsNullOrWhiteSpace(importFile), !string.IsNullOrWhiteSpace(importBase64)}.Count(x => x);
            if (specifiedCertOptionsCount == 0)
                throw new ControlledFailureException("Please specify the certificate to import.");
            
            if (specifiedCertOptionsCount > 1)
                throw new ControlledFailureException("Please specify only one of either from-registry or from-file or from-base64");

            X509Certificate2? x509Certificate = null;
            if (fromRegistry)
            {
                log.Info("Importing the Octopus 1.x certificate stored in the Windows registry...");

                var encoded = GetEncodedCertificate();
                if (encoded == null || string.IsNullOrWhiteSpace(encoded))
                {
                    throw new ControlledFailureException("No Octopus 1.x Tentacle certificate was found.");
                }
                x509Certificate = CertificateEncoder.FromBase64String(encoded, log);
            }
            else if (!string.IsNullOrWhiteSpace(importFile))
            {
                if (!File.Exists(importFile))
                    throw new ControlledFailureException($"Certificate '{importFile}' was not found.");

                var fileExtension = Path.GetExtension(importFile);

                //We assume if the file does not end in .pfx that it is the legacy base64 encoded certificate, however if this fails we should still attempt to read as the PFX format.
                if (fileExtension.ToLower() != ".pfx")
                {
                    try
                    {
                        log.Info($"Importing the certificate stored in {importFile}...");
                        var encoded = File.ReadAllText(importFile, Encoding.UTF8);
                        x509Certificate = CertificateEncoder.FromBase64String(encoded, log);
                    }
                    catch (FormatException)
                    {
                        x509Certificate = CertificateEncoder.FromPfxFile(importFile, importPfxPassword, log);
                    }
                }
                else
                {
                    x509Certificate = CertificateEncoder.FromPfxFile(importFile, importPfxPassword, log);
                }
            }
            else if (!string.IsNullOrWhiteSpace(importBase64))
            {
                log.Info("Importing the certificate via base64...");
                x509Certificate = CertificateEncoder.FromBase64String(importBase64, log);
            }

            if (x509Certificate == null)
                throw new Exception("Failed to retrieve certificate with the parameters specified.");

            tentacleConfiguration.Value.ImportCertificate(x509Certificate);
            VoteForRestart();

            if (x509Certificate.GetRSAPrivateKey()?.KeySize < CertificateGenerator.RecommendedKeyBitLength)
                log.Warn("The imported certificate's private key is smaller than the currently-recommended bit length; generating a new key for the tentacle is advised.");

            log.Info($"Certificate with thumbprint {x509Certificate.Thumbprint} imported successfully.");
        }

        string? GetEncodedCertificate()
        {
#pragma warning disable CA1416
            const RegistryHive hive = RegistryHive.LocalMachine;
            const RegistryView view = RegistryView.Registry64;
            const string keyName = "Software\\Octopus";

            using var key = RegistryKey.OpenBaseKey(hive, view);
            using var subkey = key.OpenSubKey(keyName, false);
            return (string?)subkey?.GetValue("Cert-cn=Octopus Tentacle", null);
#pragma warning restore CA1416
        }
    }
}