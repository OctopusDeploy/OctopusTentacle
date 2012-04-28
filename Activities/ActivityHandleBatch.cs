using System;
using System.Collections.ObjectModel;

namespace Octopus.Shared.Activities
{
    public class ActivityHandleBatch : IActivityHandleBatch
    {
        readonly IActivityState[] activityStates;

        public ActivityHandleBatch(IActivityState[] activityStates)
        {
            this.activityStates = activityStates;
        }

        public ReadOnlyCollection<IActivityState> Activities
        {
            get { return Array.AsReadOnly(activityStates); }
        } 

        public void WaitForCompletion()
        {
            foreach (var child in activityStates)
            {
                child.WaitForComplete();
            }
        }
    }
}