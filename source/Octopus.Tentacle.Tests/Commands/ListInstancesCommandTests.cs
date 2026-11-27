using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Tests.Commands.Support;

namespace Octopus.Tentacle.Tests.Commands
{
    public class ListInstancesCommandTests
    {
        [Test]
        public void CommandShouldReturnJsonIfRequested()
        {
            var command = CommandBuilder.ListInstances();

            command.Format = "json";
            var json = command.GetOutput(new List<ApplicationInstanceRecord> { new ApplicationInstanceRecord("MyInstance", "MyConfigPath") });
            var definition = new[] { new { InstanceName = "", ConfigurationFilePath = "" } };
            var reconstituted = JsonConvert.DeserializeAnonymousType(json, definition);
            Assert.That(reconstituted!.Length, Is.EqualTo(1));
            Assert.That(reconstituted[0].InstanceName, Is.EqualTo("MyInstance"));
            Assert.That(reconstituted[0].ConfigurationFilePath, Is.EqualTo("MyConfigPath"));
        }

        [Test]
        public void CommandShouldReturnTextIfRequested()
        {
            var command = CommandBuilder.ListInstances();

            command.Format = "text";
            var result = command.GetOutput(new List<ApplicationInstanceRecord> { new ApplicationInstanceRecord("MyInstance", "MyConfigPath") });
            Assert.That(result, Is.EqualTo("Instance 'MyInstance' uses configuration 'MyConfigPath'." + Environment.NewLine));
        }
    }
}
