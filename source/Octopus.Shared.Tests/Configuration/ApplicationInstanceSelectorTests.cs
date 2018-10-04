using System;
using System.Collections.Generic;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    public class ApplicationInstanceSelectorTests
    {
        [Test]
        public void LoadInstanceThrowsWhenNoInstanceNamePassedAndNoInstancesConfigured()
        {
            var selector = GetApplicationInstanceSelector(new List<ApplicationInstanceRecord>(), string.Empty);
            ((Action)(() => selector.LoadInstance()))
                .ShouldThrow<ControlledFailureException>()
                .WithMessage("There are no instances of OctopusServer configured on this machine. Please run the setup wizard or configure an instance using the command-line interface.");
        }

        [Test]
        public void LoadInstanceThrowsWhenNoInstanceNameIsPassedAndMoreThanOneInstance()
        {
            var instanceRecords = new List<ApplicationInstanceRecord>
            {
                new ApplicationInstanceRecord("My instance", ApplicationName.OctopusServer, "c:\\temp\\1.config"),
                new ApplicationInstanceRecord("instance 2", ApplicationName.OctopusServer, "c:\\temp\\2.config")
            };
            var selector = GetApplicationInstanceSelector(instanceRecords, string.Empty);
            ((Action)(() => selector.LoadInstance()))
                .ShouldThrow<ControlledFailureException>()
                .WithMessage("There is more than one instance of OctopusServer configured on this machine. Please pass --instance=INSTANCENAME when invoking this command to target a specific instance. Available instances: My instance, instance 2.");
        }
        
        [Test]
        public void LoadInstanceThrowsWhenNoInstanceNameIsPassedAndThereAreMultipleInstancesEvenThoughOneIsTheDefaultInstance()
        {
            var instanceRecords = new List<ApplicationInstanceRecord>
            {
                new ApplicationInstanceRecord(ApplicationInstanceRecord.GetDefaultInstance(ApplicationName.OctopusServer), ApplicationName.OctopusServer, "c:\\temp\\0.config"),
                new ApplicationInstanceRecord("My instance", ApplicationName.OctopusServer, "c:\\temp\\1.config"),
                new ApplicationInstanceRecord("instance 2", ApplicationName.OctopusServer, "c:\\temp\\2.config")
            };
            var selector = GetApplicationInstanceSelector(instanceRecords, string.Empty);
            ((Action)(() => selector.LoadInstance()))
                .ShouldThrow<ControlledFailureException>()
                .WithMessage("There is more than one instance of OctopusServer configured on this machine. Please pass --instance=INSTANCENAME when invoking this command to target a specific instance. Available instances: OctopusServer, My instance, instance 2.");
        }
        
        [Test]
        public void LoadInstanceReturnsOnlyInstanceWhenNoInstanceNameIsPassedAndOnlyOneInstance()
        {
            var instanceRecords = new List<ApplicationInstanceRecord> { new ApplicationInstanceRecord("instance 2", ApplicationName.OctopusServer, "c:\\temp\\2.config") };
            var selector = GetApplicationInstanceSelector(instanceRecords, string.Empty);
            
            selector.LoadInstance().InstanceName.Should().Be("instance 2");
        }
        
        [Test]
        public void LoadInstanceThrowsWhenInstanceNameNotFound()
        {
            var instanceRecords = new List<ApplicationInstanceRecord> { new ApplicationInstanceRecord("instance 2", ApplicationName.OctopusServer, "c:\\temp\\2.config") };
            var selector = GetApplicationInstanceSelector(instanceRecords, "instance 1");

            ((Action)(() => selector.LoadInstance()))
                .ShouldThrow<ControlledFailureException>()
                .WithMessage("Instance instance 1 of OctopusServer has not been configured on this machine. Available instances: instance 2.");
        }

        [Test]
        public void LoadInstanceMatchesInstanceNameCaseInsensitively()
        {
            var instanceRecords = new List<ApplicationInstanceRecord> { new ApplicationInstanceRecord("Instance 2", ApplicationName.OctopusServer, "c:\\temp\\2.config") };
            var selector = GetApplicationInstanceSelector(instanceRecords, "INSTANCE 2");
            
            selector.LoadInstance().InstanceName.Should().Be("Instance 2");
        }

        [Test]
        public void LoadInstanceMatchesInstanceNameCaseSensitivelyWhenOneOfThemIsAnExactMatch()
        {
            var instanceRecords = new List<ApplicationInstanceRecord>
            {
                new ApplicationInstanceRecord("Instance 2", ApplicationName.OctopusServer, "c:\\temp\\2a.config"),
                new ApplicationInstanceRecord("INSTANCE 2", ApplicationName.OctopusServer, "c:\\temp\\2b.config")
            };
            var selector = GetApplicationInstanceSelector(instanceRecords, "INSTANCE 2");
            
            selector.LoadInstance().InstanceName.Should().Be("INSTANCE 2");
        }

        [Test]
        public void LoadInstanceThrowsWhenMultipleCaseInsensitiveMatchesButNoExactMatch()
        {
            //note: this could only happen:
            // - once we've stopped using the registry as the instance store
            // - on a case sensitive operating system (ie linux or mac)
            // - where the instance configuration was done manually (as `create-instance` blocks it)

            var instanceRecords = new List<ApplicationInstanceRecord>
            {
                new ApplicationInstanceRecord("Instance 2", ApplicationName.OctopusServer, "c:\\temp\\2a.config"),
                new ApplicationInstanceRecord("INSTANCE 2", ApplicationName.OctopusServer, "c:\\temp\\2b.config")
            };
            var selector = GetApplicationInstanceSelector(instanceRecords, "instance 2");
            
            ((Action)(() => selector.LoadInstance()))
                .ShouldThrow<ControlledFailureException>()
                .WithMessage("Instance instance 2 of OctopusServer could not be matched to one of the existing instances: Instance 2, INSTANCE 2.");
        }

        [Test]
        public void CreateInstanceDoesNotAllowMultipleInstancesThatDifferByCase()
        {
            var instanceRecords = new List<ApplicationInstanceRecord>
            {
                new ApplicationInstanceRecord("Instance 2", ApplicationName.OctopusServer, "c:\\temp\\2a.config"),
            };
            var selector = GetApplicationInstanceSelector(instanceRecords, "instance 2");
            ((Action)(() => selector.CreateInstance("INSTANCE 2", "c:\\temp\\2b.config", "c:\\temp\\2b")))
                .ShouldThrow<ControlledFailureException>()
                .WithMessage("Instance Instance 2 of OctopusServer already exists on this machine, using configuration file c:\\temp\\2a.config.");
        }

        private static ApplicationInstanceSelector GetApplicationInstanceSelector(List<ApplicationInstanceRecord> instanceRecords, string currentInstanceName)
        {
            var instanceStore = Substitute.For<IApplicationInstanceStore>();
            instanceStore.ListInstances(Arg.Any<ApplicationName>()).Returns(instanceRecords);
            var selector = new ApplicationInstanceSelector(ApplicationName.OctopusServer, currentInstanceName, Substitute.For<IOctopusFileSystem>(), instanceStore, Substitute.For<ILog>());
            return selector;
        }
    }
}