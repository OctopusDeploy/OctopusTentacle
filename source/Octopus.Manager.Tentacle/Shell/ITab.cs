using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Octopus.Manager.Tentacle.Shell
{
    public interface ITab
    {
        bool IsNextEnabled { get; }
        event Action OnNavigateNext;
        Task OnNext(CancelEventArgs e);
        void OnBack(CancelEventArgs e);
    }
}