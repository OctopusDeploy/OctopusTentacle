using System;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Support.TestAttributes;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class HowManyParallelTests : IntegrationTest
    {
        [Test]
        public void HowManyTestsAreRunningInParallel()
        {
            // Only exists to make it easy to find out how many tests are running in parallel.
            Logger.Information("The number of parallel tests are: {LevelOfParallelism} on a machine with {NumberOfCpuCores} cpu cores.", 
                CustomLevelOfParallelismAttribute.LevelOfParallelism(),
                Environment.ProcessorCount);
        }
    }
}