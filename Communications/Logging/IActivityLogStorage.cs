using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Octopus.Shared.Communications.Logging
{
    public interface IActivityLogStorage
    {
        Task AppendAsync(LogMessage logMessage);
        Task<IList<ActivityLogTreeNode>> GetLogAsync(string correlationId);
    }
}