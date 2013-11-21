using System;
using System.Threading;
using Octopus.Platform.Deployment.Conventions;
using Octopus.Platform.Deployment.Packages;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Variables;

namespace Octopus.Shared.Conventions
{
    public class ConventionContext : IConventionContext
    {
        readonly ILog log;
        readonly CancellationToken cancellationToken;
        readonly Action<CreatedArtifact> storeCreatedArtifact;
        readonly PackageMetadata package;
        readonly VariableDictionary variables;

        public ConventionContext(PackageMetadata package, string directoryPath,
            VariableDictionary variables, ILog log, CancellationToken cancellationToken,
            Action<CreatedArtifact> storeCreatedArtifact)
        {
            this.package = package;
            this.variables = variables;
            this.log = log;
            this.cancellationToken = cancellationToken;
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