using System;
using System.Collections.Generic;
using Octopus.Shared.Logging;
using Octopus.Shared.Messages.Conversations;
using Octopus.Shared.Packages;
using Octopus.Shared.Variables;

namespace Octopus.Shared.Messages.Deploy.Package
{
    [ExpectReply]
    public class TentacleDeployPackageCommand : IReusableMessage
    {
        public PackageMetadata Package { get; private set; }
        public IList<Variable> Variables { get; private set; }
        public LoggerReference Logger { get; private set; }

        public TentacleDeployPackageCommand(LoggerReference logger, PackageMetadata package, IList<Variable> variables)
        {
            Package = package;
            Variables = variables;
            Logger = logger;
        }

        public IReusableMessage CopyForReuse(LoggerReference newLogger)
        {
            return new TentacleDeployPackageCommand(newLogger, Package, Variables);
        }
    }
}
