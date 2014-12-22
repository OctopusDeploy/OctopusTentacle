using System;
using System.Collections.Generic;
using Octopus.Client.Model;
using Octopus.Shared.Logging;
using Octopus.Shared.Messages.Conversations;
using Octopus.Shared.Messages.Deploy.Steps;

namespace Octopus.Shared.Messages.Deploy.Collect
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
