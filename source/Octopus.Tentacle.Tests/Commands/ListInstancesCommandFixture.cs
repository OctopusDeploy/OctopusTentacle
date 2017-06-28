using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using Octopus.Shared.Configuration;
using Octopus.Tentacle.Commands;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class ListInstancesCommandFixture : CommandFixture<ListInstancesCommand>
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            Command = new ListInstancesCommand(Substitute.For<IApplicationInstanceStore>());
        }

        [Test]
        public void CommandShouldReturnJsonIfRequested()
        {
            var json = Command.GetOutput("json", new List<ApplicationInstanceRecord> { new ApplicationInstanceRecord("MyInstance", ApplicationName.Tentacle, "MyConfigPath") });
            var definition = new[] { new { InstanceName = "", ConfigurationFilePath = "" } };
            var reconstituted = JsonConvert.DeserializeAnonymousType(json, definition);
            Assert.That(reconstituted.Length, Is.EqualTo(1));
            Assert.That(reconstituted[0].InstanceName, Is.EqualTo("MyInstance"));
            Assert.That(reconstituted[0].ConfigurationFilePath, Is.EqualTo("MyConfigPath"));
        }

        [Test]
        public void CommandShouldReturnTextIfRequested()
        {
            var result = Command.GetOutput("json", new List<ApplicationInstanceRecord> { new ApplicationInstanceRecord("MyInstance", ApplicationName.Tentacle, "MyConfigPath") });
            Assert.That(result, Is.EqualTo("Instance 'MyInstance' uses configuration 'MyConfigPath'." + Environment.NewLine));
        }
    }
}
