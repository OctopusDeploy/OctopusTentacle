using System;

namespace Octopus.Shared.Diagnostics
{
    public interface ILogScope
    {
        void Log(string text);
        void Close();
    }
}