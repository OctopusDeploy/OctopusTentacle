using System;
using System.ComponentModel;

namespace Octopus.Manager.Tentacle.Shell
{
    public interface ITab
    {
        bool IsNextEnabled { get; }
        event Action OnNavigateNext;
        void OnNext(CancelEventArgs e);
        void OnBack(CancelEventArgs e);
    }
}