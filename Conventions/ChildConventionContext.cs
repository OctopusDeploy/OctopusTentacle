using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Octopus.Shared.Contracts;
using Octopus.Shared.Integration.Scripting;
using Octopus.Shared.Orchestration.Logging;

namespace Octopus.Shared.Conventions
{
    public class ChildConventionContext : IConventionContext
    {
        readonly IConventionContext root;
        readonly ILog log;

        public ChildConventionContext(IConventionContext root, ILog log)
        {
            this.root = root;
            this.log = log;
        }

        public PackageMetadata Package { get { return root.Package; } }
        public CancellationToken CancellationToken { get { return root.CancellationToken; } }
        public X509Certificate2 Certificate { get { return root.Certificate; } }
        public string PackageContentsDirectoryPath { get { return root.PackageContentsDirectoryPath; } set { root.PackageContentsDirectoryPath = value; } }
        public string StagingDirectoryPath { get { return root.StagingDirectoryPath; } }
        public VariableDictionary Variables { get { return root.Variables; } }
        public ILog Log { get { return log; } }

        public IConventionContext ScopeTo(IConvention convention)
        {
            return new ChildConventionContext(root, log.BeginOperation(convention.FriendlyName));
        }

        public void AddCreatedArtifact(CreatedArtifact artifact)
        {
            root.AddCreatedArtifact(artifact);
        }
    }
}