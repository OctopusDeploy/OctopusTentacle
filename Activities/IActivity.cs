using System.Threading.Tasks;

namespace Octopus.Shared.Activities
{
    public interface IActivity<in TActivityMessage> where TActivityMessage : IActivityMessage
    {
        IActivityLog Log { get; set; }
        IActivityRuntime Runtime { get; set; }

        Task Execute(TActivityMessage message);
    }
}