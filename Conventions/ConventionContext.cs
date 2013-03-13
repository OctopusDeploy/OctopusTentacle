using System;
using System.Collections.Generic;
using System.Threading;
using Octopus.Shared.Activities;
using Octopus.Shared.Contracts;

namespace Octopus.Shared.Conventions
{
    public class ConventionContext
    {
        readonly IActivityLog log;
        readonly CancellationToken cancellationToken;
        readonly PackageMetadata package;
        readonly VariableDictionary variables;

        public ConventionContext(PackageMetadata package, string directoryPath, IActivityLog log) 
            : this(package, directoryPath, new VariableDictionary(new List<Variable>()), log, new CancellationToken())
        {
        }

        public ConventionContext(PackageMetadata package, string directoryPath, VariableDictionary variables, IActivityLog log, CancellationToken cancellationToken)
        {
            this.package = package;
            this.variables = variables;
            this.log = log;
            this.cancellationToken = cancellationToken;
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

        public IActivityLog Log
        {
            get { return log; }
        }

        public ConventionContext ScopeTo(IConvention convention)
        {
            return new ConventionContext(package, PackageContentsDirectoryPath, variables, new PrefixedActivityLogDecorator("[" + convention.FriendlyName + "] ", Log), cancellationToken);
        }
    }
}