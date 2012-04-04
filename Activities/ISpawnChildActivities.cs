using System;

namespace Octopus.Shared.Activities
{
    public interface ISpawnChildActivities
    {
        IActivityRuntime Runtime { get; set; }
    }
}