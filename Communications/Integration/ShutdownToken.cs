using System;

namespace Octopus.Shared.Communications.Integration
{
    // The PassiveTentacleSquidFinder does long-running work (probing
    // tentacles) while handling a message. This delays shutdown
    // of the app- practically indefinitely if there are a lot
    // of unresponsive machines to probe. This class provides a
    // signal that the PTSF and others can use to be notified of the
    // desire to shut down. Refactoring the actors involved, or adding
    // capabilities to Pipefish, may be better in the long term.
    //
    // This approach might be extended to cover the need to shut down
    // the clock thread (notes in ActivitySpaceStarter.cs).
    public class ShutdownToken
    {
        volatile bool isShutdownRequested;

        public bool IsShutdownRequested { get { return isShutdownRequested; } }

        public void RequestShutdown()
        {
            isShutdownRequested = true;
        }
    }
}
