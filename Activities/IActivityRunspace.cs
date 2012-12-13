using System;

namespace Octopus.Shared.Activities
{
    public interface IActivityRunspace
    {
        IActivityState StartActivity(IActivityMessage activity);
    }
}