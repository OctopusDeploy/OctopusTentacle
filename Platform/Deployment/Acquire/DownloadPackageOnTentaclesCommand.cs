using System;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Deployment.Acquire
{
    public class DownloadPackageOnTentaclesCommand : IMessageWithLogger
    {
        public DownloadPackageOnTentaclesCommand(LoggerReference logger)
        {
            Logger = logger;
        }

        public LoggerReference Logger { get; private set; }
    }
}