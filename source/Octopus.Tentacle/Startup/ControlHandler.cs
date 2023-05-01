using System;
using System.Runtime.InteropServices;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Startup
{
    public class ControlHandler : IDisposable
    {
        private Action OnShutDown;
#if FULL_FRAMEWORK
        private CtrlSignaling.HandlerRoutine hr;
#endif

        public ControlHandler(Action onShutDown, ISystemLog log)
        {
            OnShutDown = onShutDown;
            
            
#if FULL_FRAMEWORK
                /*
                 * The handler raises under the following conditions:
                 *  - Ctrl+C (CTRL_C_EVENT)
                 *  - Closing Window (CTRL_CLOSE_EVENT)
                 *  - Docker Stop (CTRL_SHUTDOWN_EVENT)
                 */
                hr = new CtrlSignaling.HandlerRoutine(type =>
                {
                    log.Trace("Shutdown signal received: " + type);
                    onShutDown();
                    return true;
                });
                CtrlSignaling.SetConsoleCtrlHandler(hr, true);
#else
            Console.CancelKeyPress += (s, e) =>
            {
                //SIGINT (ControlC) and SIGQUIT (ControlBreak)
                log.Trace("CancelKeyPress signal received: " + e.SpecialKey);
                onShutDown();
            };
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                //SIGTERM - i.e. Docker Stop
                log.Trace("AppDomain process exiting");
                onShutDown();
            };
#endif
        }

        public void Dispose()
        {
#if FULL_FRAMEWORK
            // Is this required?
            GC.KeepAlive(hr);
#endif
        }
    }
    
#if FULL_FRAMEWORK
        public static class CtrlSignaling
        {
            public delegate bool HandlerRoutine(CtrlTypes CtrlType);

            public enum CtrlTypes
            {
                CTRL_C_EVENT = 0,
                CTRL_BREAK_EVENT = 1,
                CTRL_CLOSE_EVENT = 2,
                CTRL_LOGOFF_EVENT = 5,
                CTRL_SHUTDOWN_EVENT = 6
            }

            [DllImport("Kernel32.dll")]
            public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);
        }
#endif
}