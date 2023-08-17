using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support.SetupFixtures
{
    public interface ISetupFixture
    {
        public void OneTimeSetUp(ILogger logger);
        
        public void OneTimeTearDown(ILogger logger);
    }
}