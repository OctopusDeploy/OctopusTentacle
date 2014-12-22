using System;
using Octopus.Shared.Logging;
using Octopus.Shared.Messages.Conversations;
using Octopus.Shared.Packages;

namespace Octopus.Shared.Messages.Agentless
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
