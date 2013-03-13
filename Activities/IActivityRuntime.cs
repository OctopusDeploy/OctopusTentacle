using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace Octopus.Shared.Activities
{
    public interface IActivityRuntime
    {
        TaskFactory TaskFactory { get; }
        void EnsureNotCancelled();
        CancellationToken CancellationToken { get; }
        Task Execute(IActivityMessage activity);
        Task Execute(IEnumerable<IActivityMessage> activities);
        Task Execute(IEnumerable<IActivityMessage> activities, int maxParallelismForChildTasks);
    }
}
