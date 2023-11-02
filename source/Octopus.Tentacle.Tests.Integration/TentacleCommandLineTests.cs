using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Exceptions;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class TentacleCommandLineTests : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task TentacleExeNoArguments(TentacleConfigurationTestCase tc)
        {
            var (exitCode, stdout, stderr) = await RunCommand(tc, "");
            
            exitCode.Should().Be(2, "the exit code should be 2 if the command wasn't understood");
            stdout.Should().StartWithEquivalentOf("Usage: Tentacle <command> [<options>]", "should show help by default if no other commands are specified");
            stdout.Should().ContainEquivalentOf("Or use <command> --help for more details.", "should provide the hint for command-specific help");
            stderr.Should().BeNullOrEmpty();
            await Task.CompletedTask;
        }
        
        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task UnknownCommand(TentacleConfigurationTestCase tc)
        {
            var (exitCode, stdout, stderr) = await RunCommand(tc, "unknown-command");
            
            exitCode.Should().Be(2, "the exit code should be 2 if the command wasn't understood");
            stderr.Should().StartWithEquivalentOf("Command 'unknown-command' is not supported", "the error should clearly indicate the command which is not understood");
            stdout.Should().StartWithEquivalentOf("See 'Tentacle help'", "should provide the hint to use help");
            await Task.CompletedTask;
        }     
        
        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task UnknownArgument(TentacleConfigurationTestCase tc)
        {
            var (exitCode, stdout, stderr) = await RunCommand(tc, "version --unknown=argument");
            
            exitCode.Should().Be(1, "the exit code should be 1 if the command has unknown arguments");
            stdout.Should().BeNullOrEmpty("the error message should be written to stderr, not stdout");
            stderr.Should().ContainEquivalentOf("Unrecognized command line arguments: --unknown=argument", "the error message (written to stderr) should clearly indicate which arguments couldn't be parsed.");
            await Task.CompletedTask;
        }
        
        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task InvalidArgument(TentacleConfigurationTestCase tc)
        {
            var (exitCode, stdout, stderr) = await RunCommand(tc, "version --format=unsupported");
            
            exitCode.Should().Be(1, "the exit code should be 1 if the command has unknown arguments");
            stdout.Should().BeNullOrEmpty("the error message should be written to stderr, not stdout");
            stderr.Should().ContainEquivalentOf("The format 'unsupported' is not supported. Try text or json.", "the error message (written to stderr) should clearly indicate which argument was invalid.");
            await Task.CompletedTask;
        }
        
        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task NoConsoleLoggingSwitchStillSilentlySupportedForBackwardsCompat(TentacleConfigurationTestCase tc)
        {
            var (_, _, stderr) = await RunCommandWithValidExitCodeAssert(tc, "version --noconsolelogging");

            stderr.Should().BeNullOrEmpty();
            await Task.CompletedTask;
        }
        
        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task NoLogoSwitchStillSilentlySupportedForBackwardsCompat(TentacleConfigurationTestCase tc)
        {
            var (_, _, stderr) = await RunCommandWithValidExitCodeAssert(tc, "version --nologo");

            stderr.Should().BeNullOrEmpty();
            await Task.CompletedTask;
        }

        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task ConsoleSwitchStillSilentlySupportedForBackwardsCompat(TentacleConfigurationTestCase tc)
        {
            var (_, _, stderr) = await RunCommandWithValidExitCodeAssert(tc, "version --console");

            stderr.Should().BeNullOrEmpty();
            await Task.CompletedTask;
        }
        
        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task ShouldSupportFuzzyCommandParsing(TentacleConfigurationTestCase tc)
        {
            await RunCommandWithValidExitCodeAssert(tc, "version");
            await RunCommandWithValidExitCodeAssert(tc, "--version");
            await RunCommandWithValidExitCodeAssert(tc, "/version");
        }

        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task VersionCommandTextFormat(TentacleConfigurationTestCase tc)
        {
            var (_, stdout, stderr) = await RunCommandWithValidExitCodeAssert(tc, "version");

            var expectedVersion = GetVersionInfo(tc);

            stdout.Should().Be(expectedVersion.ProductVersion, "The version command should print the informational version as text");
            stderr.Should().BeNullOrEmpty();
            await Task.CompletedTask;
        }

        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task VersionCommandJsonFormat(TentacleConfigurationTestCase tc)
        {
            var (_, stdout, stderr) = await RunCommandWithValidExitCodeAssert(tc, "version --format=json");

            var expectedVersion = GetVersionInfo(tc);
            var output = JObject.Parse(stdout);

            output["InformationalVersion"].Value<string>().Should().Be(expectedVersion.ProductVersion, "The version command should print the informational version in the JSON output");
            output["MajorMinorPatch"].Value<string>().Should().Be($"{expectedVersion.FileMajorPart}.{expectedVersion.FileMinorPart}.{expectedVersion.FileBuildPart}", "The version command should print the version in the json output");
            output["NuGetVersion"].Value<string>().Should().NotBeNull("The version command should print the NuGet version in the JSON output");
            output["SourceBranchName"].Value<string>().Should().NotBeNull("The version command should print the source branch in the JSON output");

            stderr.Should().BeNullOrEmpty();
            await Task.CompletedTask;
        }
        
        
        
        // [Test]
        // [TentacleConfigurations(testCommonVersions: true)]
        // public async Task CanGetHelpForHelp()
        // {
        //     RunCommand("help --help", out var stdout, out var stderr);
        //     stderr.Should().BeNullOrEmpty();
        //     assentRunner.AssentUnScrubbedContent(this, stdout);
        //     await Task.CompletedTask;
        // }
        //
        // [Test]
        // [TentacleConfigurations(testCommonVersions: true)]
        // public async Task HelpAsSwitchShouldShowCommandSpecificHelp()
        // {
        //     RunCommand("version --help", out var stdout, out var stderr);
        //     stderr.Should().BeNullOrEmpty();
        //     assentRunner.AssentUnScrubbedContent(this, stdout);
        //     await Task.CompletedTask;
        // }

        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task GeneralHelpAsJsonCanBeParsedByAutomationScripts(TentacleConfigurationTestCase tc)
        {
            var (_, stdout, stderr) = await RunCommandWithValidExitCodeAssert(tc, "help --format=json");

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

            help.Commands.Select(c => c.Name)
                .Should()
                .Contain(
                    "configure",
                    "help",
                    "run",
                    "version",
                    "show-master-key",
                    "show-thumbprint");

            await Task.CompletedTask;
        }

        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task CommandSpecificHelpAsJsonCanBeParsedByAutomationScripts(TentacleConfigurationTestCase tc)
        {
            var (_, stdout, stderr) = await RunCommandWithValidExitCodeAssert(tc, "version --help --format=json");

            stderr.Should().BeNullOrEmpty();
            var help = JsonConvert.DeserializeAnonymousType(
                stdout,
                new
                {
                    Name = "",
                    Description = "",
                    Aliases = new string[0],
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

            await Task.CompletedTask;
        }

        // [Test]
        // [TentacleConfigurations(testCommonVersions: true)]
        // public async Task CommandSpecificHelpAsJsonLooksSensibleToHumans()
        // {
        //     RunCommand("version --help --format=json", out var stdout, out var stderr);
        //     stderr.Should().BeNullOrEmpty();
        //     assentRunner.AssentUnScrubbedContent(this, stdout);
        //     await Task.CompletedTask;
        // }

        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task HelpForInstanceSpecificCommandsAlwaysWorks(TentacleConfigurationTestCase tc)
        {
            // Get all the commands using command-line - talk about dog fooding!
            var (_, stdout, stderr) = await RunCommand(tc, "help --format=json");

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

            var failed = help.Commands.Select(async c =>
                    {
                        var (exitCode2, stdout2, stderr2) = await RunCommand(tc,$"{c.Name} --help");
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
                // Log.Error(JsonConvert.SerializeObject(failed));
                Assert.Fail($"The following commands cannot show help without specifying the --instance argument:{Environment.NewLine}" + $"{string.Join(Environment.NewLine, failed.Select(x => x.Result.Command.Name))}{Environment.NewLine}" + "The details are logged above. These commands probably need to take Lazy<T> dependencies so they can be instantiated for showing help without requiring every dependency to be resolvable.");
            }

            await Task.CompletedTask;
        }
        
        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task InvalidInstance(TentacleConfigurationTestCase tc)
        {
            var (exitCode, stdout, stderr) = await RunCommand(tc, "show-thumbprint --instance=invalidinstance");
            
            exitCode.Should().Be(1, $"the exit code should be 1 if the instance is not able to be resolved");
            stderr.Should().ContainEquivalentOf("Instance invalidinstance of tentacle has not been configured", "the error message should make it clear the instance has not been configured");
            stderr.Should().ContainEquivalentOf("Available instances:", "should provide a hint as to which instances are available on the machine");
            stdout.Should().BeNullOrEmpty();
            await Task.CompletedTask;
        }

        // [Test]
        // [TentacleConfigurations(testCommonVersions: true)]
        public async Task ShowThumbprintCommandText(TentacleConfigurationTestCase tc)
        {
            string instanceName = $"test-instance-for-{nameof(ShowThumbprintCommandText)}";
            await using var clientAndTentacle = await tc.CreateBuilder().WithInstanceName(instanceName).Build(CancellationToken);
            var (exitCode, stdout, stderr) = await RunCommandWithValidExitCodeAssert(tc, $"show-thumbprint --instance={instanceName}");

            exitCode.Should().Be(0, $"we expected the command to succeed.\r\nStdErr: '{stderr}'\r\nStdOut: '{stdout}'");
            stdout.Should().Be(Support.Certificates.TentaclePublicThumbprint, "the thumbprint should be written directly to stdout");
            stderr.Should().BeNullOrEmpty();
            await Task.CompletedTask;
        }

        // [Test]
        // [TentacleConfigurations(testCommonVersions: true)]
        public async Task ShowThumbprintCommandJson(TentacleConfigurationTestCase tc)
        {
            string instanceName = $"test-instance-for-{nameof(ShowThumbprintCommandJson)}";
            await using var clientAndTentacle = await tc.CreateBuilder().WithInstanceName(instanceName).Build(CancellationToken);
            var (exitCode, stdout, stderr) = await RunCommandWithValidExitCodeAssert(tc, $"show-thumbprint --instance={instanceName} --format=json");
            
            exitCode.Should().Be(0, $"we expected the command to succeed.\r\nStdErr: '{stderr}'\r\nStdOut: '{stdout}'");
            stdout.Should().Be(JsonConvert.SerializeObject(new { Support.Certificates.TentaclePublicThumbprint }), "the thumbprint should be written directly to stdout as JSON");
            stderr.Should().BeNullOrEmpty();
            await Task.CompletedTask;
        }

        // [Test]
        // [TentacleConfigurations(testCommonVersions: true)]
        public async Task ListInstancesCommandText(TentacleConfigurationTestCase tc)
        {
            string instanceName = $"test-instance-for-{nameof(ListInstancesCommandText)}";
            await using var clientAndTentacle = await tc.CreateBuilder().WithInstanceName(instanceName).Build(CancellationToken);
            var (exitCode, stdout, stderr) = await RunCommandWithValidExitCodeAssert(tc, $"list-instances --format=text");
            
            exitCode.Should().Be(0, $"we expected the command to succeed.\r\nStdErr: '{stderr}'\r\nStdOut: '{stdout}'");
            stdout.Should().ContainEquivalentOf($"Instance '{instanceName}' uses configuration '{clientAndTentacle.RunningTentacle.HomeDirectory}'.", "the current instance should be listed");
            stderr.Should().BeNullOrEmpty();
            await Task.CompletedTask;
        }

        // [Test]
        // [TentacleConfigurations(testCommonVersions: true)]
        public async Task ListInstancesCommandJson(TentacleConfigurationTestCase tc)
        {
            string instanceName = $"test-instance-for-{nameof(ListInstancesCommandJson)}";
            await using var clientAndTentacle = await tc.CreateBuilder().WithInstanceName(instanceName).Build(CancellationToken);
            var (exitCode, stdout, stderr) = await RunCommandWithValidExitCodeAssert(tc, $"list-instances --format=json");
            
            stdout.Should().Contain($"\"InstanceName\": \"{instanceName}\"", "the current instance should be listed");
            var jsonFormattedPath = clientAndTentacle.RunningTentacle.HomeDirectory.Replace(@"\", @"\\");
            stdout.Should().Contain($"\"ConfigurationFilePath\": \"{jsonFormattedPath}\"", "the path to the config file for the current instance should be listed");
            stderr.Should().BeNullOrEmpty();
            await Task.CompletedTask;
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

        async Task<(int, string, string)> RunCommandWithValidExitCodeAssert(TentacleConfigurationTestCase tentacleConfigurationTestCase, string input)
        {
            var (exitCode, stdout, stderr) = await RunCommand(tentacleConfigurationTestCase, input);
            exitCode.Should().Be(0, $"we expected the command to succeed.\r\nStdErr: '{stderr}'\r\nStdOut: '{stdout}'");
            return (exitCode, stdout, stderr);
        }

        async Task<(int, string, string)> RunCommand(TentacleConfigurationTestCase tentacleConfigurationTestCase, string input)
        {
            var tentacleExe = TentacleExeFinder.FindTentacleExe(tentacleConfigurationTestCase.TentacleRuntime);
            var args = input;
            var output = new StringBuilder();
            var errorOut = new StringBuilder();
            
            var result = await RetryHelper.RetryAsync<CommandResult, CommandExecutionException>(
                () => Cli.Wrap(tentacleExe)
                    .WithArguments(args)
                    .WithValidation(CommandResultValidation.None)
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(errorOut))
                    .ExecuteAsync(CancellationToken));

            return (result.ExitCode, output.ToString(), errorOut.ToString());
        }
    }
}