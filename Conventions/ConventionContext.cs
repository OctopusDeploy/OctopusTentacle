using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Octopus.Platform.Deployment.Conventions;
using Octopus.Platform.Deployment.Packages;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Variables;
using Octopus.Shared.Contracts;
using Octopus.Shared.Integration.Scripting;

namespace Octopus.Shared.Conventions
{
    public class ConventionContext : IConventionContext
    {
        readonly ILog log;
        readonly CancellationToken cancellationToken;
        readonly X509Certificate2 certificate;
        readonly Action<CreatedArtifact> storeCreatedArtifact;
        readonly PackageMetadata package;
        readonly VariableDictionary variables;

        public ConventionContext(PackageMetadata package, string directoryPath,
            VariableDictionary variables, ILog log, CancellationToken cancellationToken,
            X509Certificate2 certificate, Action<CreatedArtifact> storeCreatedArtifact)
        {
            this.package = package;
            this.variables = variables;
            this.log = log;
            this.cancellationToken = cancellationToken;
            this.certificate = certificate;
            this.storeCreatedArtifact = storeCreatedArtifact;
            PackageContentsDirectoryPath = directoryPath;
            StagingDirectoryPath = directoryPath;
        }

        public PackageMetadata Package
        {
            get { return package; }
        }

        public CancellationToken CancellationToken
        {
            get { return cancellationToken; }
        }

        public X509Certificate2 Certificate
        {
            get { return certificate; }
        }

        public string PackageContentsDirectoryPath { get; set; }

        public string StagingDirectoryPath { get; private set; }

        public VariableDictionary Variables
        {
            get { return variables; }
        }

        public ILog Log
        {
            get { return log; }
        }

        public IConventionContext ScopeTo(IConvention convention)
        {
            return new ChildConventionContext(this, Log.BeginOperation(convention.FriendlyName));
        }

        public void AddCreatedArtifact(CreatedArtifact artifact)
        {
            storeCreatedArtifact(artifact);
        }
    }
}