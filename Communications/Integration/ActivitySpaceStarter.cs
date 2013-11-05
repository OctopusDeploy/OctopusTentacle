using System;
using Octopus.Platform.Diagnostics;
using Pipefish;
using Pipefish.Core;
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
            
            // Stop the clock thread.
            // It would be nice to detach all actors automatically when disposing the space,
            // but the concurrency involved requires some more careful thought.
            IActor clock;
            if (space.TryGetActor(WellKnownActors.Clock, out clock))
                space.Detach(clock);

            space.Dispose();
        }
    }
}
