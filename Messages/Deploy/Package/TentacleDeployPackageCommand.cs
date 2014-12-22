using System;
using System.Collections.Generic;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Messages.Conversations;
using Octopus.Platform.Deployment.Packages;
using Octopus.Platform.Variables;

namespace Octopus.Platform.Deployment.Messages.Deploy.Package
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
