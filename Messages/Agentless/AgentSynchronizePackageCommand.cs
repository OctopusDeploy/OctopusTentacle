using System;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Messages.Conversations;
using Octopus.Platform.Deployment.Packages;

namespace Octopus.Platform.Deployment.Messages.Agentless
{
    [ExpectReply]
    public class AgentSynchronizePackageCommand : IReusableMessage
    {
        public LoggerReference Logger { get; private set; }
        public PackageMetadata Package { get; private set; }

        public AgentSynchronizePackageCommand(
            LoggerReference logger,
            PackageMetadata package)
        {
            Logger = logger;
            Package = package;
        }

        public IReusableMessage CopyForReuse(LoggerReference newLogger)
        {
            return new AgentSynchronizePackageCommand(newLogger, Package);
        }
    }
}
