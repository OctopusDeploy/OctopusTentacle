using System;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Threading;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Shared.Util;
using Octopus.Shared.Variables;

namespace Octopus.Shared.Tests.Util
{
    [TestFixture]
    public class SilentProcessRunnerFixture
    {
        [Test]
        public void ExitCode_ShouldBeReturned()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var command = "cmd.exe";
                var arguments = @"/c exit 9999";
                var workingDirectory = "";
                var networkCredential = default(NetworkCredential);

                var exitCode = Execute(command, arguments, workingDirectory, out var debugMessages, out var infoMessages, out var errorMessages, networkCredential, cts.Token);

                exitCode.Should().Be(9999, "our custom exit code should be reflected");
                debugMessages.ToString().Should().ContainEquivalentOf($"Starting {command} in  as {WindowsIdentity.GetCurrent().Name}");
                errorMessages.ToString().Should().BeEmpty("no messages should be written to stderr");
                infoMessages.ToString().Should().BeEmpty("no messages should be written to stdout");
            }
        }

        [Test]
        public void DebugLogging_ShouldContainDiagnosticsInfo_ForDefault()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var command = "cmd.exe";
                var arguments = @"/c echo hello";
                var workingDirectory = "";
                var networkCredential = default(NetworkCredential);

                var exitCode = Execute(command, arguments, workingDirectory, out var debugMessages, out var infoMessages, out var errorMessages, networkCredential, cts.Token);

                exitCode.Should().Be(0, "the process should have run to completion");
                debugMessages.ToString().Should().ContainEquivalentOf(command, "the command should be logged")
                    .And.ContainEquivalentOf(WindowsIdentity.GetCurrent().Name, "the current user details should be logged");
                infoMessages.ToString().Should().ContainEquivalentOf("hello");
                errorMessages.ToString().Should().BeEmpty("no messages should be written to stderr");
            }
        }

        [Test]
        public void DebugLogging_ShouldContainDiagnosticsInfo_DifferentUser()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (var user = new TransientUserPrincipal())
            {
                var command = "cmd.exe";
                var arguments = @"/c echo %userdomain%\%username%";
                // Target the CommonApplicationData folder since this is a place the particular user can get to
                var workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                var networkCredential = new NetworkCredential(user.UserName, user.Password, user.DomainName);

                var exitCode = Execute(command, arguments, workingDirectory, out var debugMessages, out var infoMessages, out var errorMessages, networkCredential, cts.Token);

                exitCode.Should().Be(0, "the process should have run to completion");
                debugMessages.ToString().Should().ContainEquivalentOf(command, "the command should be logged")
                    .And.ContainEquivalentOf($@"{user.DomainName}\{user.UserName}", "the custom user details should be logged")
                    .And.ContainEquivalentOf(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "the working directory should be logged");
                infoMessages.ToString().Should().ContainEquivalentOf($@"{user.DomainName}\{user.UserName}");
                errorMessages.ToString().Should().BeEmpty("no messages should be written to stderr");
            }
        }

        [Test]
        public void RunningAsDifferentUser_ShouldCopySpecialEnvironmentVariales()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (var user = new TransientUserPrincipal())
            {
                // Set the environment variable as part of this process
                var tentacleHome = "TestTentacleHome";
                Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleHome, tentacleHome);
                
                var command = "cmd.exe";
                var arguments = $@"/c echo %{EnvironmentVariables.TentacleHome}%";
                // Target the CommonApplicationData folder since this is a place the particular user can get to
                var workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                var networkCredential = new NetworkCredential(user.UserName, user.Password, user.DomainName);

                var exitCode = Execute(command, arguments, workingDirectory, out var debugMessages, out var infoMessages, out var errorMessages, networkCredential, cts.Token);

                exitCode.Should().Be(0, "the process should have run to completion");
                infoMessages.ToString().Should().ContainEquivalentOf(tentacleHome, "the environment variable should have been copied to the child process");
                errorMessages.ToString().Should().BeEmpty("no messages should be written to stderr");
            }
        }

        [Test]
        public void CancellationToken_ShouldForceKillTheProcess()
        {
            // Terminate the process after a very short time so the test doesn't run forever
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                // Starting a new instance of cmd.exe will run indefinitely waiting for user input
                var command = "cmd.exe";
                var arguments = "";
                var workingDirectory = "";
                var networkCredential = default(NetworkCredential);

                var exitCode = Execute(command, arguments, workingDirectory, out var debugMessages, out var infoMessages, out var errorMessages, networkCredential, cts.Token);

                exitCode.Should().BeLessOrEqualTo(0, "the process should have been terminated");
                infoMessages.ToString().Should().ContainEquivalentOf("Microsoft Windows", "the default command-line header would be written to stdout");
                errorMessages.ToString().Should().BeEmpty("no messages should be written to stderr");
            }
        }

        [Test]
        public void EchoHello_ShouldWriteToStdOut()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var command = "cmd.exe";
                var arguments = @"/c echo hello";
                var workingDirectory = "";
                var networkCredential = default(NetworkCredential);

                var exitCode = Execute(command, arguments, workingDirectory, out var debugMessages, out var infoMessages, out var errorMessages, networkCredential, cts.Token);

                exitCode.Should().Be(0, "the process should have run to completion");
                errorMessages.ToString().Should().BeEmpty("no messages should be written to stderr");
                infoMessages.ToString().Should().ContainEquivalentOf("hello");
            }
        }

        [Test]
        public void EchoError_ShouldWriteToStdErr()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var command = "cmd.exe";
                var arguments = @"/c echo Something went wrong! 1>&2";
                var workingDirectory = "";
                var networkCredential = default(NetworkCredential);

                var exitCode = Execute(command, arguments, workingDirectory, out var debugMessages, out var infoMessages, out var errorMessages, networkCredential, cts.Token);

                exitCode.Should().Be(0, "the process should have run to completion");
                infoMessages.ToString().Should().BeEmpty("no messages should be written to stdout");
                errorMessages.ToString().Should().ContainEquivalentOf("Something went wrong!");
            }
        }

        [Test]
        public void RunAsCurrentUser_ShouldWork()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var command = "cmd.exe";
                var arguments = @"/c echo %userdomain%\%username%";
                var workingDirectory = "";
                var networkCredential = default(NetworkCredential);

                var exitCode = Execute(command, arguments, workingDirectory, out var debugMessages, out var infoMessages, out var errorMessages, networkCredential, cts.Token);

                exitCode.Should().Be(0, "the process should have run to completion");
                errorMessages.ToString().Should().BeEmpty("no messages should be written to stderr");
                infoMessages.ToString().Should().ContainEquivalentOf($@"{Environment.UserDomainName}\{Environment.UserName}");
            }
        }

        [Test]
        public void RunAsDifferentUser_ShouldWork()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (var user = new TransientUserPrincipal())
            {
                var command = "cmd.exe";
                var arguments = @"/c echo %userdomain%\%username%";
                // Target the CommonApplicationData folder since this is a place the particular user can get to
                var workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                var networkCredential = new NetworkCredential(user.UserName, user.Password, user.DomainName);

                var exitCode = Execute(command, arguments, workingDirectory, out var debugMessages, out var infoMessages, out var errorMessages, networkCredential, cts.Token);

                exitCode.Should().Be(0, "the process should have run to completion");
                errorMessages.ToString().Should().BeEmpty("no messages should be written to stderr");
                infoMessages.ToString().Should().ContainEquivalentOf($@"{user.DomainName}\{user.UserName}");
            }
        }

        private static int Execute(string command, string arguments, string workingDirectory, out StringBuilder debugMessages, out StringBuilder infoMessages, out StringBuilder errorMessages, NetworkCredential networkCredential, CancellationToken cancel)
        {
            var debug = new StringBuilder();
            var info = new StringBuilder();
            var error = new StringBuilder();
            var exitCode = SilentProcessRunner.ExecuteCommand(
                command,
                arguments,
                workingDirectory, 
                x =>
                {
                    Console.WriteLine($"{DateTime.UtcNow} DBG: {x}");
                    debug.Append(x);
                },
                x =>
                {
                    Console.WriteLine($"{DateTime.UtcNow} INF: {x}");
                    info.Append(x);
                },
                x =>
                {
                    Console.WriteLine($"{DateTime.UtcNow} ERR: {x}");
                    error.Append(x);
                },
                networkCredential, cancel);

            debugMessages = debug;
            infoMessages = info;
            errorMessages = error;

            return exitCode;
        }

        class TransientUserPrincipal : IDisposable
        {
            readonly PrincipalContext principalContext;
            readonly UserPrincipal principal;
            readonly string password;

            public TransientUserPrincipal(string name = null, string password = "Password01!", ContextType contextType = ContextType.Machine)
            {
                // We have seen cases where the random username is invalid - trying again should help reduce false-negatives
                // System.DirectoryServices.AccountManagement.PrincipalOperationException : The specified username is invalid.
                var attempts = 0;
                while (true)
                {
                    try
                    {
                        attempts++;
                        principalContext = new PrincipalContext(contextType);
                        {
                            principal = new UserPrincipal(principalContext);
                            principal.Name = name ?? new string(Guid.NewGuid().ToString("N").ToLowerInvariant().Where(char.IsLetter).ToArray());
                            principal.SetPassword(password);
                            principal.Save();
                            this.password = password;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to create the Windows User Account called '{principal.Name}': {ex.Message}");
                        if (attempts >= 5) throw;
                    }
                }
            }

            public string NTAccountName => principal.Sid.Translate(typeof(NTAccount)).ToString();
            public string DomainName => NTAccountName.Split(new[] {'\\'}, 2)[0];
            public string UserName => NTAccountName.Split(new[] {'\\'}, 2)[1];
            public string SamAccountName => principal.SamAccountName;
            public string Password => password;

            public void Dispose()
            {
                principal.Delete();
                principal.Dispose();
                principalContext.Dispose();
            }
        }
    }
}