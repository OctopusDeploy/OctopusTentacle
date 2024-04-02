using System.Threading.Tasks;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support.SetupFixtures
{
    public interface ISetupFixture
    {
        public Task OneTimeSetUp(ILogger logger);

        public Task OneTimeTearDown(ILogger logger)
        {
            return Task.CompletedTask;
        }
    }
}