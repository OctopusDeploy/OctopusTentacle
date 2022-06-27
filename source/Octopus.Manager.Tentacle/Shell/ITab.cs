using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Octopus.Manager.Tentacle.Shell
{
    public interface ITab
    {
        bool IsNextEnabled { get; }
        bool IsSkipEnabled { get; }
        event Action OnNavigateNext;
        Task OnSkip(CancelEventArgs e);
        Task OnNext(CancelEventArgs e);
        void OnBack(CancelEventArgs e);
    }
}