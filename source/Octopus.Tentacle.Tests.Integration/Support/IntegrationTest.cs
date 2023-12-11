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
            var logFilePath = Path.Combine(TestContext.CurrentContext.TestDirectory, TestContext.CurrentContext.Test.ID + ".tentaclelog");

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
                        try
                        {
                            File.Delete(logFilePath);
                        }
                        catch
                        {
                        }
                    }
                }
                else
                {
                    Logger.Warning($"Unable to find Tentacle Log file at {logFilePath}");
                }
            }
            else if (File.Exists(logFilePath))
            {
                try
                {
                    File.Delete(logFilePath);
                }
                catch
                {
                }
            }

            Logger.Information("Staring Test Tearing Down");
            Logger.Information("Cancelling CancellationTokenSource");
            cancellationTokenSource?.Cancel();
            Logger.Information("Disposing CancellationTokenSource");
            cancellationTokenSource?.Dispose();
            Logger.Information("Finished Test Tearing Down");
        }
    }
}
