using System;
using System.Collections.Generic;
using Octopus.Shared.Logging;
using Octopus.Shared.Packages;

namespace Octopus.Shared.Messages.Deploy.Acquire
{
    public class DownloadAndPushPackageToTentaclesCommand : IReusableMessage
    {
        public LoggerReference Logger { get; private set; }
        public string DeploymentId { get; set; }
        public string FeedId { get; set; }
        public List<string> StepIds { get; set; }
        public PackageMetadata Package { get; set; }

        public DownloadAndPushPackageToTentaclesCommand(
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

        public IReusableMessage CopyForReuse(LoggerReference newLogger)
        {
            return new DownloadAndPushPackageToTentaclesCommand(newLogger, DeploymentId, FeedId, StepIds, Package);
        }
    }
}