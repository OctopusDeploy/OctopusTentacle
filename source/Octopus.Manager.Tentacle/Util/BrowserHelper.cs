using System;
using System.ComponentModel;
using System.Diagnostics;

namespace Octopus.Manager.Tentacle.Util
{
    public static class BrowserHelper
    {
        public static void Open(Uri uri)
        {
            try
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
            }
            catch (Win32Exception)
            {
                Process.Start(new ProcessStartInfo("IExplore.exe", uri.AbsoluteUri));
            }
        }
    }
}