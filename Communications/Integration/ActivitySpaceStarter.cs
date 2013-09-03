using System;
using Octopus.Platform.Diagnostics;
using Pipefish.Hosting;

namespace Octopus.Shared.Communications.Integration
{
    public class ActivitySpaceStarter : IActivitySpaceStarter
    {
        readonly ActivitySpace space;

        public ActivitySpaceStarter(ActivitySpace space)
        {
            this.space = space;
        }

        public void Start()
        {
            Log.Octopus().Verbose("Starting activity space");
            space.Run();
        }

        public void Stop()
        {
            Log.Octopus().Verbose("Stopping activity space");
            space.Dispose();
        }
    }
}
