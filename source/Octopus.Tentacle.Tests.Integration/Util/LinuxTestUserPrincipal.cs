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
            // We're in a synchronous test helper called from the LinuxTestUserPrincipal
            // constructor. Constructors must return synchronously, so we block on the
            // async call with .GetAwaiter().GetResult(). This is sync-over-async but is
            // safe because the NUnit test runner dispatches us on a worker thread without
            // a captured SynchronizationContext, so no deadlock.
            // See https://blog.stephencleary.com/2012/07/dont-block-on-async-code.html
            var result = commandLineInvocation.ExecuteCommandAsync().GetAwaiter().GetResult();

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
