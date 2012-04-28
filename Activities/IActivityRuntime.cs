using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace Octopus.Shared.Activities
{
    public interface IActivityRuntime
    {
        CancellationTokenSource Cancellation { get; }

        Task ExecuteChild(IActivity activity);
        Task ExecuteChildren(IEnumerable<IActivity> activities);
    }
}
