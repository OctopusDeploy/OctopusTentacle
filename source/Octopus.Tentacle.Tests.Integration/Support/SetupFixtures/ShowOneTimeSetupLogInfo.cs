using NUnit.Framework;

namespace Octopus.Tentacle.Tests.Integration.Support.SetupFixtures
{
    public class ShowOneTimeSetupLogInfo :  IntegrationTest
    {
        
        /// <summary>
        /// Teamcity doesn't seem to show these logs, it is not clear why.
        ///
        /// This exists to hack around that.
        /// </summary>
        [Test]
        public void ShowOneTimeSetupLogInfoTest()
        {
            this.Logger.Information(TentacleIntegrationSetupFixtures.OneTimeSetupLogOutput);
            TentacleIntegrationSetupFixtures.OneTimeSetupLogOutput = null;
        }
    }
}