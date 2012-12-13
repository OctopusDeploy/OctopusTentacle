using System;

namespace Octopus.Shared.Activities
{
    public abstract class ActivityMessage : IActivityMessage
    {
        public string Name { get; set; }
        public string Tag { get; set; }
    }
}