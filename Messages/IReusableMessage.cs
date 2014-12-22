using System;
using Octopus.Platform.Deployment.Logging;

namespace Octopus.Platform.Deployment.Messages
{
    // Currently the best option available for guided
    // retries to be able to sit as siblings in the
    // activity tree.
    public interface IReusableMessage : ICorrelatedMessage
    {
        IReusableMessage CopyForReuse(LoggerReference newLogger);
    }
}
