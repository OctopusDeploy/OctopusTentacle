using System;
using System.Collections.Generic;
using NSubstitute;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard;
using Octopus.Manager.Tentacle.Util;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Manager.Tentacle.Tests.Builders
{
    public class SetupTentacleWizardModelBuilder
    {
        InstanceSelectionModel instanceSelectionModel;
        ITentacleManagerInstanceIdentifierService tentacleManagerInstanceIdentifierService;
        ICommandLineRunner commandLineRunner;
        ITelemetryService? telemetryService;

        public SetupTentacleWizardModelBuilder()
        {
            var instanceStore = Substitute.For<IApplicationInstanceStore>();
            instanceSelectionModel = new InstanceSelectionModel(ApplicationName.Tentacle, instanceStore);
            const string tentacleInstanceName = "TestInstance";
            instanceSelectionModel.New(tentacleInstanceName);

            tentacleManagerInstanceIdentifierService = Substitute.For<ITentacleManagerInstanceIdentifierService>();
            tentacleManagerInstanceIdentifierService.GetIdentifier().Returns(_ => Guid.NewGuid().ToString("N"));

            commandLineRunner = Substitute.For<ICommandLineRunner>();
            commandLineRunner.Execute(Arg.Any<CommandLineInvocation>(), Arg.Any<ILog>()).Returns(true);
            commandLineRunner.Execute(Arg.Any<IEnumerable<CommandLineInvocation>>(), Arg.Any<ILog>()).Returns(true);
        }

        public SetupTentacleWizardModelBuilder WithTelemetryService(ITelemetryService telemetryService)
        {
            this.telemetryService = telemetryService;
            return this;
        }

        public SetupTentacleWizardModel Build()
        {
            var model = new SetupTentacleWizardModel(
                instanceSelectionModel,
                tentacleManagerInstanceIdentifierService,
                commandLineRunner,
                telemetryService ?? new TelemetryServiceBuilder().Build()
            );
            
            // TODO: Model should take logger in constructor
            model.ReviewAndRunScriptTabViewModel.SetLogger(new SystemLog());
            
            return model;
        }
    }
}
