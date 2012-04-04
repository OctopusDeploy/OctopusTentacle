using System;
using System.Collections.Generic;
using System.Threading;

namespace Octopus.Shared.Activities
{
    public interface IActivityRuntime
    {
        CancellationTokenSource Cancellation { get; }
        IActivityHandle Execute(IActivity activity);
        IActivityHandleBatch Execute(IEnumerable<IActivity> activities);
        IActivityHandleBatch BeginExecute(IEnumerable<IActivity> activities);
        IActivityHandle BeginExecute(IActivity activity);
    }
}
