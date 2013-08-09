using System.Threading.Tasks;
using Octopus.Shared.Orchestration.Logging;

namespace Octopus.Shared.Activities
{
    public interface IActivity<in TActivityMessage> where TActivityMessage : IActivityMessage
    {
        ITrace Log { get; set; }
        IActivityRuntime Runtime { get; set; }

        Task Execute(TActivityMessage message);
    }
}