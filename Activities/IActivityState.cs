using System;
using System.Collections.ObjectModel;
using System.Text;

namespace Octopus.Shared.Activities
{
    public interface IActivityState
    {
        string Name { get; }
        string Tag { get; }
        ActivityStatus Status { get; }
        IActivityLog Log { get; }
        Exception Error { get; }
        ReadOnlyCollection<IActivityState> Children { get; }
        bool IsComplete { get; }

        void WaitForComplete();
    }
}