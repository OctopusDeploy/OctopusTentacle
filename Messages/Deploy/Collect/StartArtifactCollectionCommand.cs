using System;
using System.Collections.Generic;
using Octopus.Client.Model;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Messages.Conversations;
using Octopus.Platform.Deployment.Messages.Deploy.Steps;
using Octopus.Platform.Model;

namespace Octopus.Platform.Deployment.Messages.Deploy.Collect
{
    [ExpectReply]
    public class StartArtifactCollectionCommand : ICorrelatedMessage
    {
        public LoggerReference Logger { get; private set; }
        public ReferenceCollection RelatedDocumentIds { get; private set; }
        public string RemoteSquid { get; set; }
        public List<TentacleArtifact> Artifacts { get; set; }

        public StartArtifactCollectionCommand(LoggerReference logger, ReferenceCollection relatedDocumentIds, string remoteSquid, List<TentacleArtifact> artifacts)
        {
            Logger = logger;
            RelatedDocumentIds = relatedDocumentIds;
            RemoteSquid = remoteSquid;
            Artifacts = artifacts;
        }
    }
}
