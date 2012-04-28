using System;
using System.Collections.ObjectModel;

namespace Octopus.Shared.Activities
{
    public interface IActivityHandleBatch
    {
        ReadOnlyCollection<IActivityState> Activities { get; }
        void WaitForCompletion(); 
    }
}