using System;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support.SetupFixtures
{
    public class TurnOnExceptionThrowingForLogFileForLatestTentacle : ISetupFixture
    {
        public void OneTimeSetUp(ILogger logger)
        {
            // This will help us find errors that normally get swallowed, and become hard to find.
            TryTurnOnExceptionThrowingForTentacleRuntime(TentacleRuntime.DotNet6, logger);
            TryTurnOnExceptionThrowingForTentacleRuntime(TentacleRuntime.Framework48, logger);
        }

        static void TryTurnOnExceptionThrowingForTentacleRuntime(TentacleRuntime runtime, ILogger logger)
        {
            TentacleNLogFile.TryAdjustForRuntime(
                runtime, 
                logger, 
                content => content.Replace("throwExceptions=\"false\"", "throwExceptions=\"true\""));
        }

        public void OneTimeTearDown(ILogger logger)
        {
        }
    }
}
