using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using Octopus.Tentacle.Tests.Integration.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public abstract class IntegrationTest
    {
        CancellationTokenSource? cancellationTokenSource;
        public CancellationToken CancellationToken { get; private set; }
        public ILogger Logger { get; private set; } = null!;

        [SetUp]
        public void SetUp()
        {
            Logger = new SerilogLoggerBuilder().Build().ForContext(GetType());
            Logger.Information("Test started");
            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken = cancellationTokenSource.Token;
        }
        
        [TearDown]
        public void TearDown()
        {
            WriteTentacleLogToOutputIfTestHasFailed();

            Logger.Information("Staring Test Tearing Down");
            Logger.Information("Cancelling CancellationTokenSource");
            cancellationTokenSource?.Cancel();
            Logger.Information("Disposing CancellationTokenSource");
            cancellationTokenSource?.Dispose();
            Logger.Information("Finished Test Tearing Down");
        }

        void WriteTentacleLogToOutputIfTestHasFailed()
        {
            var logFilePath = GetTempTentacleLogPath();

            if (TestContext.CurrentContext.Result.Outcome == ResultState.Error ||
                TestContext.CurrentContext.Result.Outcome == ResultState.Failure)
            {
                if (File.Exists(logFilePath))
                {
                    try
                    {
                        var tentacleLog = File.ReadAllText(logFilePath);
                        Logger.Information("############################################");
                        Logger.Information("#########    START TENTACLE LOG    #########");
                        Logger.Information("############################################");
                        Logger.Information(Environment.NewLine + tentacleLog);
                        Logger.Information("############################################");
                        Logger.Information("#########     END TENTACLE LOG     #########");
                        Logger.Information("############################################");
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Error writing Tentacle Log to test output");
                    }
                    finally
                    {
                        SafelyDeleteTempTentacleLog();
                    }
                }
                else
                {
                    Logger.Warning($"Unable to find Tentacle Log file at {logFilePath}");
                }
            }
            else
            {
                SafelyDeleteTempTentacleLog();
            }
        }

        static void SafelyDeleteTempTentacleLog()
        {
            var logFilePath = GetTempTentacleLogPath();

            if (File.Exists(logFilePath))
            {
                try
                {
                    File.Delete(logFilePath);
                }
                catch
                {
                }
            }
        }

        public static string GetTempTentacleLogPath()
        {
            return Path.Combine(TestContext.CurrentContext.TestDirectory, TestContext.CurrentContext.Test.ID + ".tentaclelog");
        }
    }
}
