using System;
using System.Collections.Generic;

namespace Octopus.Shared.Communications.Logging
{
    public interface IActivityLogStorage
    {
        void Append(LogMessage logMessage);
        IList<ActivityLogTreeNode> GetLog(string correlationId);
    }
}