using System;
using Octopus.Platform.Deployment.Logging;

namespace Octopus.Platform.Deployment.Messages.Deletion
{
    public class StartDocumentDeletionCommand : ICorrelatedMessage
    {
        public LoggerReference Logger { get; private set; }
        public string DocumentId { get; private set; }

        public StartDocumentDeletionCommand(LoggerReference logger, string documentID)
        {
            Logger = logger;
            DocumentId = documentID;
        } 
    }
}
