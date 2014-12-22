using System;
using Pipefish;
using Pipefish.Supervision;

namespace Octopus.Shared.Logging
{
    public interface ISupervisedActivity : ISupervised
    {
        IActivity Activity { get; }
        SupervisionConfiguration Configuration { get; }

        void SucceedWithInfo(string messageText);
        void SucceedWithInfo(IMessage result, string messageText);
        void SucceedWithInfoFormat(string messageFormat, params object[] args);
        void SucceedWithInfoFormat(IMessage result, string messageFormat, params object[] args);
    }
}
