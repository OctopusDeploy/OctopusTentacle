using System;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Deployment.Acquire
{
    public class DownloadAndPushPackageToTentaclesCommand : IMessageWithLogger
    {
        public DownloadAndPushPackageToTentaclesCommand(LoggerReference logger)
        {
            Logger = logger;
        }

        public LoggerReference Logger { get; private set; }
    }
}