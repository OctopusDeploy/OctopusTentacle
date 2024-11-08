using System;
using System.IO;
using System.Linq;
using System.Threading;
using Halibut.Util;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using Octopus.Client.Extensions;
using Octopus.Tentacle.Tests.Integration.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public abstract class IntegrationTest : IDisposable
    {
        CancellationTokenSource? cancellationTokenSource;
        public CancellationToken CancellationToken { get; private set; }
        public ILogger Logger { get; private set; } = null!;

        [SetUp]
        public void SetUp()
        {
            Logger = new SerilogLoggerBuilder().Build().ForContext(GetType());
            var driveInfos = DriveInfo.GetDrives();
            Logger.Information($"Test started. Available Disk space before starting: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            // Time out the cancellation token so we cancel the test if it takes too long
            // The IntegrationTestTimeout attribute will also cancel the test if it takes too long, but nunit will not call TearDown on the test 
            // in this scenario and so will fail to cancel the cancellation token and clean up the test gracefully, potentially leading to execution timeout errors
            cancellationTokenSource = new CancellationTokenSource(IntegrationTestTimeout.TestTimeoutInMilliseconds() - TimeSpan.FromSeconds(5).Milliseconds);
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
            cancellationTokenSource = null;
            var driveInfos = DriveInfo.GetDrives();
            Logger.Information($"Finished Test Tearing Down. Available Disk space before starting: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

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

        public void Dispose()
        {
            Try.CatchingError(() => cancellationTokenSource?.Cancel(), _ => {});
            Try.CatchingError(() => cancellationTokenSource?.Dispose(), _ => {});
        }
    }
}
