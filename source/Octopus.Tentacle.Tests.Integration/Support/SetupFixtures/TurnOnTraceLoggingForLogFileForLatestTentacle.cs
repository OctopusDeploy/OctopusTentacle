using System;
using System.Threading.Tasks;
using NLog;
using ILogger = Serilog.ILogger;

namespace Octopus.Tentacle.Tests.Integration.Support.SetupFixtures
{
    public class TurnOnTraceLoggingForLogFileForLatestTentacle : ISetupFixture
    {
        public async Task OneTimeSetUp(ILogger logger)
        {
            await Task.CompletedTask;

            TryTurnOnTraceLoggingForTentacleRuntime(TentacleRuntime.DotNet6, logger);
            TryTurnOnTraceLoggingForTentacleRuntime(TentacleRuntime.Framework48, logger);
        }

        void TryTurnOnTraceLoggingForTentacleRuntime(TentacleRuntime runtime, ILogger logger)
        {
            try
            {
                LogManager.Configuration.Variables["logLevel"] = "Trace";
                LogManager.ReconfigExistingLoggers();
            }
            catch (Exception e)
            {
                logger.Error(e, $"Unable to turn on Trace logging for {runtime}");
            }
        }
    }
}
