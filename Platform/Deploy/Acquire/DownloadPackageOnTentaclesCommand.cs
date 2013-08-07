using System;
using System.Collections.Generic;
using Octopus.Shared.Contracts;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Deploy.Acquire
{
    public class DownloadPackageOnTentaclesCommand : IMessageWithLogger
    {
        public LoggerReference Logger { get; private set; }
        public string DeploymentId { get; private set; }
        public string FeedId { get; private set; }
        public List<string> StepIds { get; private set; }
        public PackageMetadata Package { get; private set; }

        public DownloadPackageOnTentaclesCommand(
            LoggerReference logger, 
            string deploymentId, 
            string feedId, 
            List<string> stepIds,
            PackageMetadata package)
        {
            Logger = logger;
            DeploymentId = deploymentId;
            FeedId = feedId;
            StepIds = stepIds;
            Package = package;
        }
    }
}