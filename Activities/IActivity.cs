using System;
using System.Threading.Tasks;

namespace Octopus.Shared.Activities
{
    public interface IActivity
    {
        Task Execute();
    }

    public interface IRuntimeAware
    {
        IActivityLog Log { get; set; }
        IActivityRuntime Runtime { get; set; }
    }
}