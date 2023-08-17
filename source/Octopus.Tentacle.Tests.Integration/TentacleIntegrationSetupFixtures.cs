using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Support.SetupFixtures;
using Octopus.Tentacle.Tests.Integration.Util;

// This class must be in the top level 'Octopus.Tentacle.Tests.Integration' namespace.
// Otherwise nunit wont use it.
namespace Octopus.Tentacle.Tests.Integration
{
    /// <summary>
    /// We have just one of these since, if we have many logging sometimes doesn't work.
    /// 
    /// </summary>
    [SetUpFixture] // Must be the one and only. 
    public class TentacleIntegrationSetupFixtures
    {
        private ISetupFixture[] setupFixtures = new ISetupFixture[]
        {
            new BumpThreadPoolForAllTests(),
            new WarmTentacleCache()
        };
        
        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            var logger = new SerilogLoggerBuilder().Build().ForContext<TentacleIntegrationSetupFixtures>();
            foreach (var setupFixture in setupFixtures)
            {
                Console.WriteLine("TentacleIntegrationSetupFixtures from console");
                setupFixture.OneTimeSetUp(logger.ForContext(setupFixture.GetType()));
            }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            var logger = new SerilogLoggerBuilder().Build().ForContext<TentacleIntegrationSetupFixtures>();
            var exceptions = new List<Exception>();
            foreach (var setupFixture in setupFixtures.Reverse())
            {
                try
                {
                    setupFixture.OneTimeTearDown(logger.ForContext(setupFixture.GetType()));
                }
                catch (Exception e)
                {
                    logger.Error(e, $"{setupFixture.GetType()} throw an exception");
                    exceptions.Add(e);
                }
            }

            if (exceptions.Any())
            {
                throw new AggregateException(exceptions);
            }
        }
    }
}