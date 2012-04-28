using System;
using System.Collections.ObjectModel;
using System.Text;

namespace Octopus.Shared.Activities
{
    public interface IActivityState
    {
        string Name { get; }
        ActivityStatus Status { get; }
        StringBuilder Log { get; }
        Exception Error { get; }
        ReadOnlyCollection<IActivityState> Children { get; }
        bool IsComplete { get; }

        void WaitForComplete();
    }
}