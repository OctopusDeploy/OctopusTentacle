using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Variables;
using Octopus.Shared.Contracts;
using Octopus.Shared.Integration.Scripting;
using Octopus.Shared.Orchestration.Logging;

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
        ILog Log { get; }
        IConventionContext ScopeTo(IConvention convention);
        void AddCreatedArtifact(CreatedArtifact artifact);
    }
}