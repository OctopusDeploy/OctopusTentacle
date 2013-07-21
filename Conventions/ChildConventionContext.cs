using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Octopus.Shared.Activities;
using Octopus.Shared.Contracts;
using Octopus.Shared.Integration.Scripting;

namespace Octopus.Shared.Conventions
{
    public class ChildConventionContext : IConventionContext
    {
        readonly IConventionContext root;
        readonly IActivityLog log;

        public ChildConventionContext(IConventionContext root, IActivityLog log)
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
        public IActivityLog Log { get { return log; } }

        public IConventionContext ScopeTo(IConvention convention)
        {
            return new ChildConventionContext(root, new PrefixedActivityLogDecorator("[" + convention.FriendlyName + "] ", log));
        }

        public void AddCreatedArtifact(CreatedArtifact artifact)
        {
            root.AddCreatedArtifact(artifact);
        }
    }
}