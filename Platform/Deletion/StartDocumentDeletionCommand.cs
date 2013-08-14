using System;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Deletion
{
    public class StartDocumentDeletionCommand : IMessageWithLogger
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
