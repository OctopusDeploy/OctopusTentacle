#if NET
using System;
using System.IO;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.CommonTestUtils.Diagnostics;
using Octopus.Tentacle.Core.Util;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.TestAttributes;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Configuration
{
    [LinuxTest]
    [NonParallelizable]
    [UnsupportedOSPlatform("windows")]
    public class NonRootCreateInstanceTests : IntegrationTest
    {
        [Test]
        [RequiresSudoOnLinux]
        public async Task WhenANonRootUserCannotCreateTheKeyThenTheCommandInTheErrorFixesIt()
        {
            // Arrange
            using var user = new LinuxTestUserPrincipal("octo-key-test");
            // Under /tmp rather than the default location, which is in the runner's home and not traversable by the test user.
            using var temporaryDirectory = new TemporaryDirectory(new OctopusPhysicalFileSystem(new InMemoryLog()), Directory.CreateTempSubdirectory().FullName);
            var root = temporaryDirectory.DirectoryPath;
            File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            var dataDirectory = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDirectory);
            var homeDirectory = Path.Combine(root, "home");
            var configurationFile = Path.Combine(root, "config", "tentacle.config");
            Directory.CreateDirectory(homeDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(configurationFile)!);
            await Sudo("chown", "-R", user.UserName, homeDirectory, Path.GetDirectoryName(configurationFile)!);

            Task<BufferedCommandResult> CreateInstanceAsUser()
                => Sudo("-u", user.UserName, "env",
                    $"{EnvironmentVariables.TentacleInstancesDataDirectory}={dataDirectory}",
                    $"{EnvironmentVariables.TentacleMachineConfigurationHomeDirectory}={homeDirectory}",
                    TentacleExeFinder.FindTentacleExe(), "create-instance", "--instance", "Tentacle", "--config", configurationFile);

            // Act
            var refused = await CreateInstanceAsUser();
            Logger.Information("StdOut: {StdOut}, StdErr: {StdErr}", refused.StandardOutput, refused.StandardError);
            var fix = Regex.Match(refused.StandardOutput + refused.StandardError, "sudo mkdir[^`]*").Value;
            Logger.Information("Running Fix: {Fix}", fix);
            await Cli.Wrap("/bin/bash").WithArguments(new[] { "-c", fix }).ExecuteBufferedAsync();
            var created = await CreateInstanceAsUser();

            // Assert
            refused.ExitCode.Should().NotBe(0);
            fix.Should().NotBeEmpty($"the error should say which command fixes it, but it was: {refused.StandardOutput}{refused.StandardError}");
            created.ExitCode.Should().Be(0, $"running `{fix}` should let the user create the instance, but: {created.StandardOutput}{created.StandardError}");
            File.Exists(configurationFile).Should().BeTrue();
        }

        static Task<BufferedCommandResult> Sudo(params string[] arguments)
            => Cli.Wrap("sudo").WithArguments(arguments).WithValidation(CommandResultValidation.None).ExecuteBufferedAsync();
    }
}
#endif
