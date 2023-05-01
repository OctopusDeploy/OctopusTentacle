using System;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Support;

namespace Octopus.Tentacle.Tests.Integration
{
    public class JustTryingToGetAPollingTentacle
    {
        [Test]
        [Obsolete("Obsolete")]
        public void Doit()
        {
            using IHalibutRuntime octopus = new HalibutRuntime(Support.Certificates.Server);
            
            
            new PollingTentacleBuilder().DoStuff();
        }
    }
}