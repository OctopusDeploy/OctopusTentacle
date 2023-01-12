using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class ListInstancesCommandFixture : CommandFixture<ListInstancesCommand>
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            Command = new ListInstancesCommand(Substitute.For<IApplicationInstanceStore>(), Substitute.For<ILogFileOnlyLogger>());
        }

        [Test]
        public void CommandShouldReturnJsonIfRequested()
        {
            Command.Format = "json";
            var json = Command.GetOutput(new List<ApplicationInstanceRecord> { new ApplicationInstanceRecord("MyInstance", "MyConfigPath") });
            var definition = new[] { new { InstanceName = "", ConfigurationFilePath = "" } };
            var reconstituted = JsonConvert.DeserializeAnonymousType(json, definition);
            Assert.That(reconstituted!.Length, Is.EqualTo(1));
            Assert.That(reconstituted[0].InstanceName, Is.EqualTo("MyInstance"));
            Assert.That(reconstituted[0].ConfigurationFilePath, Is.EqualTo("MyConfigPath"));
        }

        [Test]
        public void CommandShouldReturnTextIfRequested()
        {
            Command.Format = "text";
            var result = Command.GetOutput(new List<ApplicationInstanceRecord> { new ApplicationInstanceRecord("MyInstance", "MyConfigPath") });
            Assert.That(result, Is.EqualTo("Instance 'MyInstance' uses configuration 'MyConfigPath'." + Environment.NewLine));
        }
    }
}