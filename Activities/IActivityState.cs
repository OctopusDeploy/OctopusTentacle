using System;
using System.Collections.ObjectModel;
using Octopus.Shared.Orchestration.Logging;

namespace Octopus.Shared.Activities
{
    public interface IActivityState
    {
        string Name { get; }
        string Tag { get; }
        ActivityStatus Status { get; }
        ITrace Log { get; }
        Exception Error { get; }
        ReadOnlyCollection<IActivityState> Children { get; }
        bool IsComplete { get; }
        string Id { get; }
        
        void Cancel();
        void WaitForComplete();
    }
}