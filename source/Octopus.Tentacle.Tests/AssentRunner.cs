using System;
using System.Runtime.CompilerServices;
using Assent;

namespace Octopus.Tentacle.Tests
{
    public class AssentRunner
    {
        private readonly Assent.Configuration configuration;

        public AssentRunner(Assent.Configuration configuration)
        {
            this.configuration = configuration;
        }

        public void Assent(object testFixture,
            string recieved,
            [CallerMemberName] string? testName = null,
            [CallerFilePath] string? filePath = null)
        {
            try
            {
                testFixture.Assent(recieved, configuration, testName, filePath);
            }
            catch (AssentApprovedFileNotFoundException e)
            {
                PublishArtifacts(e.ReceivedFileName);
                throw;
            }
            catch (AssentFailedException e)
            {
                PublishArtifacts(e.ReceivedFileName);
                PublishArtifacts(e.ApprovedFileName);
                throw;
            }
        }

        public static void PublishArtifacts(string file)
        {
            SendMessage("publishArtifacts", $"'{file}'");
        }

        private static void SendMessage(string type, string value)
        {
            if (!TestExecutionContext.IsRunningInTeamCity)
                return;

            Console.WriteLine($"##teamcity[{type} {value}]");
        }
    }
}