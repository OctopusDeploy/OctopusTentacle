using System;
using System.Collections.Generic;
using Octopus.Client.Model;
using Octopus.Platform.Model;
using Octopus.Shared.Platform.Conversations;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Deploy.Collect
{
    [ExpectReply]
    public class StartArtifactCollectionCommand : IMessageWithLogger
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
