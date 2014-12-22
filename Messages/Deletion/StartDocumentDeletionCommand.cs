using System;
using Octopus.Shared.Logging;

namespace Octopus.Shared.Messages.Deletion
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
