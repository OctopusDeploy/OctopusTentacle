using System;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Util
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

        // Why this is sync: RunCommand is called from the constructor, which can't
        // be async.
        //
        // Why blocking on the async call is safe: this only runs under NUnit, which
        // dispatches us on a worker thread with no SynchronizationContext.
        //
        // Why low risk: this is test code. The worst case for a wrong call here is
        // a hung test, not a production incident.
        // See https://blog.stephencleary.com/2012/07/dont-block-on-async-code.html
        static void RunCommand(string arguments, bool failOnNonZeroExitCode = true)
        {
            var commandLineInvocation = new CommandLineInvocation("/bin/bash", arguments);
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
