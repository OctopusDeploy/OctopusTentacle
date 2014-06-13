using System;
using System.Collections.Generic;
using Autofac;
using Autofac.Features.Metadata;
using Octopus.Platform.Diagnostics;
using Pipefish;
using Pipefish.Core;
using Pipefish.Hosting;
using Pipefish.Persistence;
using Pipefish.Standard;

namespace Octopus.Shared.Communications.Integration
{
    public class ActivitySpaceStarter : IActivitySpaceStarter
    {
        readonly ActivitySpace space;
        readonly ShutdownToken shutdown;

        public ActivitySpaceStarter(ActivitySpace space, ShutdownToken shutdown)
        {
            this.space = space;
            this.shutdown = shutdown;
        }

        public void Start()
        {
            Log.Octopus().Verbose("Starting activity space");
            space.Run();
        }

        public void Stop()
        {
            Log.Octopus().Verbose("Stopping activity space");

            shutdown.RequestShutdown();
            
            // Stop the clock thread.
            // It would be nice to detach all actors automatically when disposing the space,
            // but the concurrency involved requires some more careful thought.
            IActor clock;
            if (space.TryGetActor(WellKnownActors.Clock, out clock))
            {
                var disp = clock as IDisposable;
                if (disp != null)
                    disp.Dispose();
            }

            space.Dispose();
        }

        public static void LoadWellKnownActors(ActivitySpace space, IComponentContext scope)
        {
            Log.Octopus().VerboseFormat("Resolving activity space infrastructure for {0}", space.Name);

            var storage = scope.Resolve<IActorStorage>();
            foreach (var actor in scope.Resolve<IEnumerable<Meta<IActor>>>())
            {
                var actorName = (string)actor.Metadata["Name"];

                var peristent = actor.Value as IPersistentActor;
                if (peristent != null)
                {
                    var state = storage.GetStorageFor(actorName);
                    peristent.AttachStorage(state);
                }

                space.Attach(actorName, actor.Value);
            }
        }
    }
}
