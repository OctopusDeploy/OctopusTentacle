using System;
using System.Collections.Generic;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Orchestration.Logging
{
    public interface IActivityLogStorage
    {
        void Append(LogMessage logMessage);
        void Append(ProgressMessage logMessage);
        IList<ActivityLogTreeNode> GetLog(string correlationId);
    }
}