using System;
using System.IO;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Util
{
    // Creates a Linux user for the test and tries to delete it again when disposed.
    class LinuxTestUserPrincipal : IDisposable
    {
        public LinuxTestUserPrincipal(string username)
        {
            UserName = username;
            Password = Guid.NewGuid().ToString();

            RunCommand(DeleteUserCommand, failOnNonZeroExitCode: false);
            RunCommand(IsAlpine
                ? $"-c \"{Sudo}adduser -D -s /bin/sh {username}\""
                : $"-c \"{Sudo}useradd -m -c \\\"Linux test User for OctopusShared tests\\\" {username} -s /bin/bash\"", failOnNonZeroExitCode: true);
            RunCommand($"-c \"echo {username}:\"{Password}\" | {Sudo}chpasswd\"", failOnNonZeroExitCode: true);
        }

        public string Password { get; }

        public string UserName { get; }

        public void Dispose() => RunCommand(DeleteUserCommand, failOnNonZeroExitCode: false);

        // Alpine has BusyBox's adduser and deluser rather than useradd and userdel.
        static bool IsAlpine => File.Exists("/etc/alpine-release");

        static string Sudo => Environment.UserName == "root" ? "" : "sudo ";

        string DeleteUserCommand => IsAlpine ? $"-c \"{Sudo}deluser --remove-home {UserName}\"" : $"-c \"{Sudo}userdel -r {UserName}\"";

        static void RunCommand(string arguments, bool failOnNonZeroExitCode)
        {
            var commandLineInvocation = new CommandLineInvocation("/bin/sh", arguments);
            // Constructors can't be async, and NUnit runs tests without a SynchronizationContext, so blocking can't deadlock.
            var result = commandLineInvocation.ExecuteCommandAsync().GetAwaiter().GetResult();

            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine(error);
            }

            if (result.ExitCode != 0 && failOnNonZeroExitCode)
            {
                throw new ApplicationException($"Unable to create linux test user, exit code {result.ExitCode}");
            }
        }
    }
}
