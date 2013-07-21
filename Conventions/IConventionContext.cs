using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Octopus.Shared.Activities;
using Octopus.Shared.Contracts;
using Octopus.Shared.Integration.Scripting;

namespace Octopus.Shared.Conventions
{
    public interface IConventionContext
    {
        PackageMetadata Package { get; }
        CancellationToken CancellationToken { get; }
        X509Certificate2 Certificate { get; }
        string PackageContentsDirectoryPath { get; set; }
        string StagingDirectoryPath { get; }
        VariableDictionary Variables { get; }
        IActivityLog Log { get; }
        IConventionContext ScopeTo(IConvention convention);
        void AddCreatedArtifact(CreatedArtifact artifact);
    }
}