using System;
using NSubstitute;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Manager.Tentacle.Util;

namespace Octopus.Manager.Tentacle.Tests.Builders
{
    public class TelemetryServiceBuilder
    {
        bool sendingResult = true;

        public TelemetryServiceBuilder WithSendingResult(bool result)
        {
            sendingResult = result;
            return this;
        }

        public ITelemetryService Build()
        {
            var telemetryService = Substitute.For<ITelemetryService>();
            telemetryService.SendTelemetryEvent(Arg.Any<Uri>(), Arg.Any<TelemetryEvent>()).Returns(sendingResult);
            return telemetryService;
        }
    }
}
