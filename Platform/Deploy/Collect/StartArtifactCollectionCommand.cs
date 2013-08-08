using System;
using System.Collections.Generic;
using Octopus.Shared.Platform.Conversations;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Deploy.Collect
{
    [ExpectReply]
    public class StartArtifactCollectionCommand : IMessageWithLogger
    {
        public LoggerReference Logger { get; private set; }
        public string ReleaseId { get; set; }
        public string RemoteSquid { get; set; }
        public List<TentacleArtifact> Artifacts { get; set; }

        public StartArtifactCollectionCommand(LoggerReference logger, string releaseId, string remoteSquid, List<TentacleArtifact> artifacts)
        {
            Logger = logger;
            ReleaseId = releaseId;
            RemoteSquid = remoteSquid;
            Artifacts = artifacts;
        }
    }
}
