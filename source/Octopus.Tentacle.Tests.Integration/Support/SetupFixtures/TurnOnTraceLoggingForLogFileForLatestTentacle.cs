using System;
using System.IO;
using System.Threading;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support.SetupFixtures
{
    public class TurnOnTraceLoggingForLogFileForLatestTentacle : ISetupFixture
    {
        public void OneTimeSetUp(ILogger logger)
        {
            TryTurnOnTraceLoggingForTentacleRuntime(TentacleRuntime.DotNet6, logger);
            TryTurnOnTraceLoggingForTentacleRuntime(TentacleRuntime.Framework48, logger);
        }

        static void TryTurnOnTraceLoggingForTentacleRuntime(TentacleRuntime runtime, ILogger logger)
        {
            TentacleNLogFile.TryAdjustForRuntime(
                runtime, 
                logger, 
                content => content.Replace("<logger name=\"*\" minlevel=\"Info\" writeTo=\"octopus-log-file\" />", "<logger name=\"*\" minlevel=\"Trace\" writeTo=\"octopus-log-file\" />"));
        }

        public void OneTimeTearDown(ILogger logger)
        {
        }
    }
}
