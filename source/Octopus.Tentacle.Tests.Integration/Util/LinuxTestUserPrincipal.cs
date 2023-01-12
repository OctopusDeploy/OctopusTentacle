using System;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Util
{
    class LinuxTestUserPrincipal
    {
        public LinuxTestUserPrincipal(string username)
        {
            UserName = username;
            Password = Guid.NewGuid().ToString();

            RunCommand($"-c \"sudo userdel {username}\"", failOnNonZeroExitCode: false);
            RunCommand($"-c \"sudo useradd -m -c \\\"Linux test User for OctopusShared tests\\\" {username} -s /bin/bash\"");
            RunCommand($"-c \"echo {username}:\"{Password}\" | sudo chpasswd\"");
        }

        public string Password { get; }

        public string UserName { get;  }

        static void RunCommand(string arguments, bool failOnNonZeroExitCode = true)
        {
            var commandLineInvocation = new CommandLineInvocation("/bin/bash", arguments);
            var result = commandLineInvocation.ExecuteCommand();

            foreach (var line in result.Errors)
                Console.WriteLine(line);
            foreach (var error in result.Errors)
                Console.Error.WriteLine(error);
            if (result.ExitCode != 0 && failOnNonZeroExitCode)
            {
                Console.Error.WriteLine($"Creating linux test user failed with exit code: {result.ExitCode}");
                throw new ApplicationException("Unable to create linux test user");
            }
        }
    }
}
