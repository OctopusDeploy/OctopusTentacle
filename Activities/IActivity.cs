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
        IActivityRuntime Runtime { get; set; }
    }
}