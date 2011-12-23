using System;
using System.Collections.ObjectModel;

namespace Octopus.Shared.Activities
{
    public interface IActivityHandleBatch
    {
        ReadOnlyCollection<IActivityHandle> Activities { get; }
        void WaitForCompletion(); 
    }
}