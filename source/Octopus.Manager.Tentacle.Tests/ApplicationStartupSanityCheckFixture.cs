using System;
using Autofac;
using NUnit.Framework;
using Octopus.Manager.Tentacle.Shell;

namespace Octopus.Manager.Tentacle.Tests
{
    [TestFixture]
    public class ApplicationStartupSanityCheckFixture
    {
        [Test]
        public void ApplicationCanStartWithoutCrashing()
        {
            // If we can resolve the main view model without errors,
            // then we can safely assume all the necessary components
            // have been correctly registered in the IoC container
            var container = App.ConfigureContainer();
            _ = container.Resolve<ShellViewModel>();
        }
    }
}
