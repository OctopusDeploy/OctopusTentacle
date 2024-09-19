using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Exceptions;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.TestAttributes;
using Octopus.Tentacle.Util;
using Octopus.Tentacle.Variables;
using Polly;
using PlatformDetection = Octopus.Tentacle.Util.PlatformDetection;

namespace Octopus.Tentacle.Tests.Integration
{
    /// <summary>
    /// These tests provide guarantees around how our command-line interface works, especially for scenarios where people automate setup.
    /// Please review any changes to the assertions made by these tests carefully.
    /// </summary>
    [IntegrationTestTimeout]
    public class TentacleCommandLineTests : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task TentacleExeNoArguments(TentacleConfigurationTestCase tc)
        {
            var (exitCode, stdout, stderr) = await RunCommand(tc, null);
            
            exitCode.Should().Be(2, "the exit code should be 2 if the command wasn't understood");
            stdout.Should().StartWithEquivalentOf("Usage: Tentacle <command> [<options>]", "should show help by default if no other commands are specified");
            stdout.Should().ContainEquivalentOf("Or use <command> --help for more details.", "should provide the hint for command-specific help");
            stderr.Should().BeNullOrEmpty();
        }
        
        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task UnknownCommand(TentacleConfigurationTestCase tc)
        {
            var (exitCode, stdout, stderr) = await RunCommand(tc, null, "unknown-command");
            
            exitCode.Should().Be(2, "the exit code should be 2 if the command wasn't understood");
            stderr.Should().StartWithEquivalentOf("Command 'unknown-command' is not supported", "the error should clearly indicate the command which is not understood");
            stdout.Should().StartWithEquivalentOf("See 'Tentacle help'", "should provide the hint to use help");
        }     
        
        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task UnknownArgument(TentacleConfigurationTestCase tc)
        {
            var (exitCode, stdout, stderr) = await RunCommand(tc, null, "version", "--unknown=argument");
            
            exitCode.Should().Be(1, "the exit code should be 1 if the command has unknown arguments");
            stdout.Should().BeNullOrEmpty("the error message should be written to stderr, not stdout");
            stderr.Should().ContainEquivalentOf("Unrecognized command line arguments: --unknown=argument", "the error message (written to stderr) should clearly indicate which arguments couldn't be parsed.");
        }
        
        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task InvalidArgument(TentacleConfigurationTestCase tc)
        {
            var (exitCode, stdout, stderr) = await RunCommand(tc, null, "version", "--format=unsupported");
            
            exitCode.Should().Be(1, "the exit code should be 1 if the command has unknown arguments");
            stdout.Should().BeNullOrEmpty("the error message should be written to stderr, not stdout");
            stderr.Should().ContainEquivalentOf("The format 'unsupported' is not supported. Try text or json.", "the error message (written to stderr) should clearly indicate which argument was invalid.");
        }
        
        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task NoConsoleLoggingSwitchStillSilentlySupportedForBackwardsCompat(TentacleConfigurationTestCase tc)
        {
            var (_, _, stderr) = await RunCommandAndAssertExitsWithSuccessExitCode(tc, null, "version", "--noconsolelogging");

            stderr.Should().BeNullOrEmpty();
        }
        
        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task NoLogoSwitchStillSilentlySupportedForBackwardsCompat(TentacleConfigurationTestCase tc)
        {
            var (_, _, stderr) = await RunCommandAndAssertExitsWithSuccessExitCode(tc, null, "version", "--nologo");

            stderr.Should().BeNullOrEmpty();
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task ConsoleSwitchStillSilentlySupportedForBackwardsCompat(TentacleConfigurationTestCase tc)
        {
            var (_, _, stderr) = await RunCommandAndAssertExitsWithSuccessExitCode(tc, null, "version", "--console");

            stderr.Should().BeNullOrEmpty();
        }
        
        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task ShouldSupportFuzzyCommandParsing(TentacleConfigurationTestCase tc)
        {
            await RunCommandAndAssertExitsWithSuccessExitCode(tc, null, "version");
            await RunCommandAndAssertExitsWithSuccessExitCode(tc, null, "--version");
            await RunCommandAndAssertExitsWithSuccessExitCode(tc, null, "/version");
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task VersionCommandTextFormat(TentacleConfigurationTestCase tc)
        {
            var (_, stdout, stderr) = await RunCommandAndAssertExitsWithSuccessExitCode(tc, null, "version");

            var expectedVersion = GetVersionInfo(tc);

            stdout.Should().Be(expectedVersion.ProductVersion, "The version command should print the informational version as text");
            stderr.Should().BeNullOrEmpty();
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task VersionCommandJsonFormat(TentacleConfigurationTestCase tc)
        {
            var (_, stdout, stderr) = await RunCommandAndAssertExitsWithSuccessExitCode(tc, null, "version", "--format=json");

            var expectedVersion = GetVersionInfo(tc);
            var output = JObject.Parse(stdout);

            output["InformationalVersion"].Value<string>().Should().Be(expectedVersion.ProductVersion, "The version command should print the informational version in the JSON output");
            output["MajorMinorPatch"].Value<string>().Should().Be($"{expectedVersion.FileMajorPart}.{expectedVersion.FileMinorPart}.{expectedVersion.FileBuildPart}", "The version command should print the version in the json output");
            output["NuGetVersion"].Value<string>().Should().NotBeNull("The version command should print the NuGet version in the JSON output");
            output["SourceBranchName"].Value<string>().Should().NotBeNull("The version command should print the source branch in the JSON output");

            stderr.Should().BeNullOrEmpty();
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task CanGetHelpForHelp(TentacleConfigurationTestCase tc)
        {
            var (_, stdout, stderr) = await RunCommandAndAssertExitsWithSuccessExitCode(tc, null, "help", "--help");
            stderr.Should().BeNullOrEmpty();

            stdout.Should().Be(
@"Usage: Tentacle help [<options>]

Where [<options>] is any of: 

      --format=VALUE         The format of the output (text,json). Defaults 
                               to text.

Or one of the common options: 

      --help                 Show detailed help for this command
");
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task HelpAsSwitchShouldShowCommandSpecificHelp(TentacleConfigurationTestCase tc)
        {
            var (_, stdout, stderr) = await RunCommandAndAssertExitsWithSuccessExitCode(tc, null, "version", "--help");
            stderr.Should().BeNullOrEmpty();

            stdout.Should().Be(
                @"Usage: Tentacle version [<options>]

Where [<options>] is any of: 

      --format=VALUE         The format of the output (text,json). Defaults 
                               to text.

Or one of the common options: 

      --help                 Show detailed help for this command
");
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task GeneralHelpAsJsonCanBeParsedByAutomationScripts(TentacleConfigurationTestCase tc)
        {
            var (_, stdout, stderr) = await RunCommandAndAssertExitsWithSuccessExitCode(tc, null, "help", "--format=json");

            stderr.Should().BeNullOrEmpty();
            var help = JsonConvert.DeserializeAnonymousType(
                stdout,
                new
                {
                    Commands = new[]
                    {
                        new
                        {
                            Name = "",
                            Description = "",
                            Aliases = Array.Empty<string>()
                        }
                    }
                });

            help.Commands.Select(c => c.Name)
                .Should()
                .Contain(
                    "configure",
                    "help",
                    "run",
                    "version",
                    "show-master-key",
                    "show-thumbprint");
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task CommandSpecificHelpAsJsonCanBeParsedByAutomationScripts(TentacleConfigurationTestCase tc)
        {
            var (_, stdout, stderr) = await RunCommandAndAssertExitsWithSuccessExitCode(tc, null, "version", "--help", "--format=json");

            stderr.Should().BeNullOrEmpty();
            var help = JsonConvert.DeserializeAnonymousType(
                stdout,
                new
                {
                    Name = "",
                    Description = "",
                    Aliases = Array.Empty<string>(),
                    Options = new[]
                    {
                        new
                        {
                            Name = "",
                            Description = ""
                        }
                    }
                });

            help.Name.Should().Be("version");
            help.Options.Select(o => o.Name).Should().Contain("format");
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task CommandSpecificHelpAsJsonLooksSensibleToHumans(TentacleConfigurationTestCase tc)
        {
            var (_, stdout, stderr) = await RunCommandAndAssertExitsWithSuccessExitCode(tc, null, "version", "--help", "--format=json");
            stderr.Should().BeNullOrEmpty();

            stdout.Should().Be(
@"{
  ""Name"": ""version"",
  ""Description"": ""Show the Tentacle version information"",
  ""Aliases"": [],
  ""Options"": [
    {
      ""Name"": ""format"",
      ""Description"": ""The format of the output (text,json). Defaults to text.""
    }
  ],
  ""CommonOptions"": [
    {
      ""Name"": ""help"",
      ""Description"": ""Show detailed help for this command""
    }
  ]
}");
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task HelpForInstanceSpecificCommandsAlwaysWorks(TentacleConfigurationTestCase tc)
        {
            var (_, stdout, stderr) = await RunCommand(tc, null, "help", "--format=json");

            stderr.Should().BeNullOrEmpty();
            var help = JsonConvert.DeserializeAnonymousType(
                stdout,
                new
                {
                    Commands = new[]
                    {
                        new
                        {
                            Name = "",
                            Description = "",
                            Aliases = new string[0]
                        }
                    }
                });

            help.Commands.Should().HaveCountGreaterThan(0);

            var failed = help.Commands.Select(async c =>
                    {
                        var (exitCode2, stdout2, stderr2) = await RunCommand(tc, null,$"{c.Name}", "--help");
                        return new
                        {
                            Command = c,
                            ExitCode = exitCode2,
                            StdOut = stdout2,
                            StdErr = stderr2,
                            HasExpectedExitCode = exitCode2 == 0,
                            HasExpectedHelpMessage = stdout2.StartsWith($"Usage: Tentacle {c.Name} [<options>]")
                        };
                    })
                .Where(r => !(r.Result.HasExpectedExitCode && r.Result.HasExpectedHelpMessage))
                .ToArray();

            if (failed.Any())
            {
                var failureDetails = string.Empty;

                foreach (var failure in failed)
                {
                    failureDetails += $@"{failure.Result.Command.Name}
StdErr:{failure.Result.StdErr}
StdOut:{failure.Result.StdOut}";
                }

                Assert.Fail(
$@"The following commands cannot show help without specifying the --instance argument:
{failureDetails}
The details are logged above. These commands probably need to take Lazy<T> dependencies so they can be instantiated for showing help without requiring every dependency to be resolvable.");
            }
        }
        
        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        // Run these tests in serial to avoid conflicts
        [NonParallelizable]
        public async Task InvalidInstance(TentacleConfigurationTestCase tc)
        {
            var (exitCode, stdout, stderr) = await RunCommand(
                tc, 
                null,
                "show-thumbprint", "--instance=invalidinstance");
            
            exitCode.Should().Be(1, $"the exit code should be 1 if the instance is not able to be resolved");
            stderr.Should().ContainEquivalentOf("Instance invalidinstance of tentacle has not been configured", "the error message should make it clear the instance has not been configured");
            stderr.Should().ContainEquivalentOf("Available instances:", "should provide a hint as to which instances are available on the machine");
            stdout.Should().BeNullOrEmpty();
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        // Run these tests in serial to avoid conflicts
        [NonParallelizable]
        public async Task ShowThumbprintCommandText(TentacleConfigurationTestCase tc)
        {
            await using var clientAndTentacle = await tc.CreateBuilder().Build(CancellationToken);
            await clientAndTentacle.RunningTentacle.Stop(CancellationToken);
            var (exitCode, stdout, stderr) = await RunCommandAndAssertExitsWithSuccessExitCode(
                tc, 
                clientAndTentacle.RunningTentacle.RunTentacleEnvironmentVariables, 
                "show-thumbprint", $"--instance={clientAndTentacle.RunningTentacle.InstanceName}");

            exitCode.Should().Be(0, $"we expected the command to succeed.\r\nStdErr: '{stderr}'\r\nStdOut: '{stdout}'");
            stdout.Should().Be(TestCertificates.TentaclePublicThumbprint, "the thumbprint should be written directly to stdout");
            stderr.Should().BeNullOrEmpty();
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        // Run these tests in serial to avoid conflicts
        [NonParallelizable]
        public async Task ShowThumbprintCommandJson(TentacleConfigurationTestCase tc)
        {
            await using var clientAndTentacle = await tc.CreateBuilder().Build(CancellationToken);
            await clientAndTentacle.RunningTentacle.Stop(CancellationToken);
            var (exitCode, stdout, stderr) = await RunCommandAndAssertExitsWithSuccessExitCode(
                tc, 
                clientAndTentacle.RunningTentacle.RunTentacleEnvironmentVariables,
                "show-thumbprint", $"--instance={clientAndTentacle.RunningTentacle.InstanceName}", "--format=json");
            
            exitCode.Should().Be(0, $"we expected the command to succeed.\r\nStdErr: '{stderr}'\r\nStdOut: '{stdout}'");
            stdout.Should().Be(JsonConvert.SerializeObject(new { Thumbprint = TestCertificates.TentaclePublicThumbprint }), "the thumbprint should be written directly to stdout as JSON");
            stderr.Should().BeNullOrEmpty();
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        // Run these tests in serial to avoid conflicts
        [NonParallelizable]
        public async Task ListInstancesCommandText(TentacleConfigurationTestCase tc)
        {
            Logger.Information("Inside ListInstancesCommandText");

            await using (var clientAndTentacle = await tc.CreateBuilder().Build(CancellationToken))
            {
                Logger.Information("Opened clientAndTentacle. Going to call stop on apparently running tentacle");

                await clientAndTentacle.RunningTentacle.Stop(CancellationToken);
                
                Logger.Information("Finished stopping apparently running tentacle");

                Logger.Information("Listing instances");
                var (exitCode, stdout, stderr) = await RunCommandAndAssertExitsWithSuccessExitCode(
                    tc,
                    clientAndTentacle.RunningTentacle.RunTentacleEnvironmentVariables,
                    "list-instances", "--format=text");

                Logger.Information("Finished Listing instances");

                exitCode.Should().Be(0, $"we expected the command to succeed.\r\nStdErr: '{stderr}'\r\nStdOut: '{stdout}'");
                var configPath = Path.Combine(clientAndTentacle.RunningTentacle.HomeDirectory, clientAndTentacle.RunningTentacle.InstanceName + ".cfg");
                stdout.Should().Contain($"Instance '{clientAndTentacle.RunningTentacle.InstanceName}' uses configuration '{configPath}'.", "the current instance should be listed");
                stderr.Should().BeNullOrEmpty();
            }
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        // Run these tests in serial to avoid conflicts
        [NonParallelizable]
        public async Task ListInstancesCommandJson(TentacleConfigurationTestCase tc)
        {
            await using var clientAndTentacle = await tc.CreateBuilder().Build(CancellationToken);
            await clientAndTentacle.RunningTentacle.Stop(CancellationToken);
            var (_, stdout, stderr) = await RunCommandAndAssertExitsWithSuccessExitCode(
                tc, 
                clientAndTentacle.RunningTentacle.RunTentacleEnvironmentVariables, 
                "list-instances", "--format=json");
            
            stdout.Should().Contain($"\"InstanceName\": \"{clientAndTentacle.RunningTentacle.InstanceName}\"", "the current instance should be listed");
            var configPath = Path.Combine(clientAndTentacle.RunningTentacle.HomeDirectory, clientAndTentacle.RunningTentacle.InstanceName + ".cfg");
            var jsonFormattedPath = JsonFormattedPath(configPath);
            stdout.Should().Contain($"\"ConfigurationFilePath\": \"{jsonFormattedPath}\"", "the path to the config file for the current instance should be listed");
            stderr.Should().BeNullOrEmpty();
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        // Run these tests in serial to avoid conflicts
        [NonParallelizable]
        public async Task ShouldLogStartupDiagnosticsToInstanceLogFileOnly(TentacleConfigurationTestCase tc)
        {
            await using var clientAndTentacle = await tc.CreateBuilder().Build(CancellationToken);
            await clientAndTentacle.RunningTentacle.Stop(CancellationToken);

            var startingLogText = clientAndTentacle.RunningTentacle.ReadAllLogFileText();

            var (exitCode, stdout, stderr) = await RunCommandAndAssertExitsWithSuccessExitCode(
                tc, 
                clientAndTentacle.RunningTentacle.RunTentacleEnvironmentVariables, 
                "show-thumbprint", $"--instance={clientAndTentacle.RunningTentacle.InstanceName}");

            try
            {
                var logFileText = Policy
                    .Handle<NotLoggedYetException>()
                    .WaitAndRetry(
                        20,
                        i => TimeSpan.FromMilliseconds(100 * i),
                        (exception, _) => { Logger.Information($"Failed to get new log content: {exception.Message}. Retrying!"); })
                    .Execute(
                        () =>
                        {
                            var wholeLog = clientAndTentacle.RunningTentacle.ReadAllLogFileText();
                            var newLog = wholeLog.Replace(startingLogText, string.Empty);
                            if (string.IsNullOrWhiteSpace(newLog) || !newLog.Contains("CommandLine:"))
                            {
                                throw new NotLoggedYetException();
                            }
                            return newLog;
                        });
                
                logFileText.Should().ContainEquivalentOf($"OperatingSystem: {RuntimeInformation.OSDescription}", "the OSVersion should be in our diagnostics");
                logFileText.Should().ContainEquivalentOf("OperatingSystem:", "the OSVersion should be in our diagnostics");
                logFileText.Should().ContainEquivalentOf($"OsBitVersion: {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}", "the OsBitVersion should be in our diagnostics");
                logFileText.Should().ContainEquivalentOf($"Is64BitProcess: {Environment.Is64BitProcess}", "the Is64BitProcess should be in our diagnostics");

                if (PlatformDetection.IsRunningOnWindows)
                {
#pragma warning disable CA1416
                    logFileText.Should().ContainEquivalentOf($"CurrentUser: {WindowsIdentity.GetCurrent().Name}", "the CurrentUser should be in our diagnostics");
#pragma warning disable CA1416
                }
                else
                {
                    logFileText.Should().ContainEquivalentOf($"CurrentUser: {Environment.UserName}", "the CurrentUser should be in our diagnostics");
                }
                
                logFileText.Should().ContainEquivalentOf($"MachineName: {Environment.MachineName}", "the MachineName should be in our diagnostics");
                logFileText.Should().ContainEquivalentOf($"ProcessorCount: {Environment.ProcessorCount}", "the ProcessorCount should be in our diagnostics");
                logFileText.Should().ContainEquivalentOf($"CurrentDirectory: {Directory.GetCurrentDirectory()}", "the CurrentDirectory should be in our diagnostics");
                logFileText.Should().ContainEquivalentOf($"TempDirectory: {Path.GetTempPath()}", "the TempDirectory should be in our diagnostics");
                logFileText.Should().ContainEquivalentOf("HostProcessName: ", "the HostProcessName should be in our diagnostics");
                stdout.Should().NotContainEquivalentOf($"{RuntimeInformation.OSDescription}", "the OSVersion should not be written to stdout");
                await Task.CompletedTask;
            }
            catch (NotLoggedYetException)
            {
                Logger.Error("Failed to get new log content");
                Logger.Error($"Process exit code {exitCode}");
                Logger.Error($"Starting log text: {Environment.NewLine}{startingLogText}");
                Logger.Error($"Current log text: {Environment.NewLine}{clientAndTentacle.RunningTentacle.ReadAllLogFileText()}");
                Logger.Error($"Command StdOut: {Environment.NewLine}{stdout}");
                Logger.Error($"Command StdErr: {Environment.NewLine}{stderr}");

                throw;
            }
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task HelpAsFirstArgumentShouldShowCommandSpecificHelp(TentacleConfigurationTestCase tc)
        {
            var (_, stdout, stderr) = await RunCommandAndAssertExitsWithSuccessExitCode(tc, null, "help", "version");
            stderr.Should().BeNullOrEmpty();

            stdout.Should().Be(
@"Usage: Tentacle version [<options>]

Where [<options>] is any of: 

      --format=VALUE         The format of the output (text,json). Defaults 
                               to text.

Or one of the common options: 

      --help                 Show detailed help for this command
");
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        // Run these tests in serial to avoid conflicts
        [NonParallelizable]
        public async Task ShowConfigurationCommand(TentacleConfigurationTestCase tc)
        {
            await using var clientAndTentacle = await tc.CreateBuilder().Build(CancellationToken);
            await clientAndTentacle.RunningTentacle.Stop(CancellationToken);
            var (_, stdout, stderr) = await RunCommandAndAssertExitsWithSuccessExitCode(
                tc, 
                clientAndTentacle.RunningTentacle.RunTentacleEnvironmentVariables, 
                "show-configuration", $"--instance={clientAndTentacle.RunningTentacle.InstanceName}");

            stderr.Should().BeNullOrEmpty();

            // Actually parse and query the document just like our consumer will
            dynamic? settings = JsonConvert.DeserializeObject(stdout);

            ((string)settings.Octopus.Home).Should().Be(clientAndTentacle.RunningTentacle.HomeDirectory, "the home directory should match");
            ((string)settings.Tentacle.Deployment.ApplicationDirectory).Should().Be(clientAndTentacle.RunningTentacle.ApplicationDirectory, "the application directory should match");
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        // Run these tests in serial to avoid conflicts
        [NonParallelizable]
        public async Task ShowConfigurationCommandOnPartiallyConfiguredTentacle(TentacleConfigurationTestCase tc)
        {
            using var homeDirectory = new TemporaryDirectory();
            var environmentVariables = new Dictionary<string, string?> { { EnvironmentVariables.TentacleMachineConfigurationHomeDirectory, homeDirectory.DirectoryPath } };

            var instanceId = Guid.NewGuid().ToString();
            using var temporaryDirectory = new TemporaryDirectory();
            await RunCommandAndAssertExitsWithSuccessExitCode(
                tc, 
                environmentVariables,
                "create-instance", $"--instance={instanceId}", "--config", Path.Combine(temporaryDirectory.DirectoryPath, instanceId + ".cfg"));

            var (_, stdout, stderr) = await RunCommandAndAssertExitsWithSuccessExitCode(
                tc, 
                environmentVariables,
                "show-configuration", 
                $"--instance={instanceId}");

            stderr.Should().BeNullOrEmpty();

            // Actually parse and query the document just like our consumer will
            dynamic? settings = JsonConvert.DeserializeObject(stdout);
            ((string)settings.Octopus.Home).Should().Be(temporaryDirectory.DirectoryPath, "the home directory should match");
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        // Run these tests in serial to avoid conflicts
        [NonParallelizable]
        public async Task ShowConfigurationCommandLooksSensibleToHumans(TentacleConfigurationTestCase tc)
        {
            await using var clientAndTentacle = await tc.CreateBuilder().Build(CancellationToken);
            await clientAndTentacle.RunningTentacle.Stop(CancellationToken);
            var (_, stdout, stderr) = await RunCommandAndAssertExitsWithSuccessExitCode(
                tc, 
                clientAndTentacle.RunningTentacle.RunTentacleEnvironmentVariables,
                "show-configuration", $"--instance={clientAndTentacle.RunningTentacle.InstanceName}");

            stderr.Should().BeNullOrEmpty();

            if (tc.TentacleType == TentacleType.Polling)
            {
                stdout.Should().Be($@"{{
  ""Octopus"": {{
    ""Home"": ""{JsonFormattedPath(clientAndTentacle.RunningTentacle.HomeDirectory)}"",
    ""Watchdog"": {{
      ""Enabled"": false,
      ""Instances"": ""*"",
      ""Interval"": 0
    }}
  }},
  ""Tentacle"": {{
    ""CertificateThumbprint"": ""{clientAndTentacle.RunningTentacle.Thumbprint}"",
    ""Communication"": {{
      ""TrustedOctopusServers"": [
        {{
          ""Thumbprint"": ""{clientAndTentacle.Server.Thumbprint}"",
          ""CommunicationStyle"": 2,
          ""Address"": ""https://localhost:{clientAndTentacle.Server.ServerListeningPort}"",
          ""Squid"": null,
          ""SubscriptionId"": ""{clientAndTentacle.RunningTentacle.ServiceUri}""
        }}
      ]
    }},
    ""Deployment"": {{
      ""ApplicationDirectory"": ""{JsonFormattedPath(clientAndTentacle.RunningTentacle.ApplicationDirectory)}""
    }},
    ""Services"": {{
      ""ListenIP"": null,
      ""NoListen"": true,
      ""PortNumber"": 10933
    }}
  }}
}}
");
            }
            else
            {
                stdout.Should().Be($@"{{
  ""Octopus"": {{
    ""Home"": ""{JsonFormattedPath(clientAndTentacle.RunningTentacle.HomeDirectory)}"",
    ""Watchdog"": {{
      ""Enabled"": false,
      ""Instances"": ""*"",
      ""Interval"": 0
    }}
  }},
  ""Tentacle"": {{
    ""CertificateThumbprint"": ""{clientAndTentacle.RunningTentacle.Thumbprint}"",
    ""Communication"": {{
      ""TrustedOctopusServers"": [
        {{
          ""Thumbprint"": ""{clientAndTentacle.Server.Thumbprint}"",
          ""CommunicationStyle"": 1,
          ""Address"": null,
          ""Squid"": null,
          ""SubscriptionId"": null
        }}
      ]
    }},
    ""Deployment"": {{
      ""ApplicationDirectory"": ""{JsonFormattedPath(clientAndTentacle.RunningTentacle.ApplicationDirectory)}""
    }},
    ""Services"": {{
      ""ListenIP"": null,
      ""NoListen"": false,
      ""PortNumber"": {clientAndTentacle.RunningTentacle.ServiceUri.Port}
    }}
  }}
}}
");
            }

            await Task.CompletedTask;
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        [WindowsTest]
        // Run these tests in serial to avoid conflicts
        [NonParallelizable]
        public async Task WatchdogCreateAndDeleteCommand(TentacleConfigurationTestCase tc)
        {
            await using var clientAndTentacle = await tc.CreateBuilder().Build(CancellationToken);
            await clientAndTentacle.RunningTentacle.Stop(CancellationToken);
            var create = await RunCommandAndAssertExitsWithSuccessExitCode(
                tc, 
                clientAndTentacle.RunningTentacle.RunTentacleEnvironmentVariables,
                "watchdog", "--create", $"--instances={clientAndTentacle.RunningTentacle.InstanceName}");

            create.StdError.Should().BeNullOrEmpty();
            create.StdOut.Should().ContainEquivalentOf("Creating watchdog task");
            var delete = await RunCommandAndAssertExitsWithSuccessExitCode(
                tc, 
                clientAndTentacle.RunningTentacle.RunTentacleEnvironmentVariables,
                "watchdog", "--delete", $"--instances={clientAndTentacle.RunningTentacle.InstanceName}");

            delete.StdError.Should().BeNullOrEmpty();
            delete.StdOut.Should().ContainEquivalentOf("Removing watchdog task");
        }
        
        FileVersionInfo GetVersionInfo(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var tentacleExe = TentacleExeFinder.FindTentacleExe(tentacleConfigurationTestCase.TentacleRuntime);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return FileVersionInfo.GetVersionInfo(tentacleExe);
            }

            //todo: change this to trust the value set in the context.TentacleExePath (will need a renovation of ExePathResolver to be non windows specific)
            return FileVersionInfo.GetVersionInfo($"{tentacleExe}.dll");
        }

        async Task<(int ExitCode, string StdOut, string StdError)> RunCommandAndAssertExitsWithSuccessExitCode(
            TentacleConfigurationTestCase tentacleConfigurationTestCase, 
            IReadOnlyDictionary<string, string?>? environmentVariables,
            params string[] arguments)
        {
            var (exitCode, stdout, stderr) = await RunCommand(tentacleConfigurationTestCase, environmentVariables, arguments);
            exitCode.Should().Be(0, $"we expected the command to succeed.\r\nStdErr: '{stderr}'\r\nStdOut: '{stdout}'");
            return (exitCode, stdout, stderr);
        }

        async Task<(int ExitCode, string StdOut, string StdError)> RunCommand(
            TentacleConfigurationTestCase tentacleConfigurationTestCase, 
            IReadOnlyDictionary<string, string?>? environmentVariables,
            params string[] arguments)
        {
            using var tempDirectory = new TemporaryDirectory();

            var environmentVariablesToRunTentacleWith = new Dictionary<string, string?>();

            if (environmentVariables?.Any() == true)
            {
                environmentVariablesToRunTentacleWith.AddRange(environmentVariables);
            }

            if (!environmentVariablesToRunTentacleWith.ContainsKey(EnvironmentVariables.TentacleMachineConfigurationHomeDirectory))
            {
                environmentVariablesToRunTentacleWith.Add(EnvironmentVariables.TentacleMachineConfigurationHomeDirectory, tempDirectory.DirectoryPath);
            }

            var tentacleExe = TentacleExeFinder.FindTentacleExe(tentacleConfigurationTestCase.TentacleRuntime);
            var output = new StringBuilder();
            var errorOut = new StringBuilder();
            
            var result = await RetryHelper.RetryAsync<CommandResult, CommandExecutionException>(
                () => Cli.Wrap(tentacleExe)
                    .WithArguments(arguments)
                    .WithValidation(CommandResultValidation.None)
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(errorOut))
                    .WithEnvironmentVariables(environmentVariablesToRunTentacleWith)
                    .ExecuteAsync(CancellationToken));

            return (result.ExitCode, output.ToString(), errorOut.ToString());
        }

        static string JsonFormattedPath(string path)
        {
            return path.Replace(@"\", @"\\");
        }

        public class NotLoggedYetException : Exception
        {
        }
    }
}
