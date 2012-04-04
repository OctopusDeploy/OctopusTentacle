using System;
using System.Collections.ObjectModel;

namespace Octopus.Shared.Activities
{
    public class ActivityHandleBatch : IActivityHandleBatch
    {
        readonly IActivityHandle[] activityHandles;

        public ActivityHandleBatch(IActivityHandle[] activityHandles)
        {
            this.activityHandles = activityHandles;
        }

        public ReadOnlyCollection<IActivityHandle> Activities
        {
            get { return Array.AsReadOnly(activityHandles); }
        } 

        public void WaitForCompletion()
        {
            foreach (var child in activityHandles)
            {
                child.WaitForComplete();
            }
        }
    }
}