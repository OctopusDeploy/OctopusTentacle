using System;
using System.Collections.Generic;
using Octopus.Shared.Contracts;
using Octopus.Shared.Platform.Conversations;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Deploy.Package
{
    [ExpectReply]
    public class TentacleDeployPackageCommand : IMessageWithLogger
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
    }
}
