using System;
using Autofac;
using Octopus.Shared.Diagnostics;
using Pipefish.Hosting;

namespace Octopus.Shared.Communications
{
    public class ActivitySpaceStarter : IStartable
    {
        readonly ActivitySpace space;

        public ActivitySpaceStarter(ActivitySpace space)
        {
            this.space = space;
        }

        public void Start()
        {
            Log.Octopus().Debug("Starting activity space");
            space.Run();
        }
    }
}
