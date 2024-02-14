using System;
using System.IO;
using System.Threading;
using NLog;
using ILogger = Serilog.ILogger;

namespace Octopus.Tentacle.Tests.Integration.Support.SetupFixtures
{
    public class TurnOnTraceLoggingForLogFileForLatestTentacle : ISetupFixture
    {
        private CancellationTokenSource cts = new();

        public void OneTimeSetUp(ILogger logger)
        {
            TryTurnOnTraceLoggingForTentacleRuntime(TentacleRuntime.DotNet6, logger);
            TryTurnOnTraceLoggingForTentacleRuntime(TentacleRuntime.Framework48, logger);
        }

        void TryTurnOnTraceLoggingForTentacleRuntime(TentacleRuntime runtime, ILogger logger)
        {
            try
            {
                LogManager.Configuration.Variables["logLevel"] = "Trace";
            }
            catch (Exception e)
            {
                logger.Error(e, $"Unable to turn on Trace logging for {runtime}");
            }
        }
        
        public void OneTimeTearDown(ILogger logger)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }
}
