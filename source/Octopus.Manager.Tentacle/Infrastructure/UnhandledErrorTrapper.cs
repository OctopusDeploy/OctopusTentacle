using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Octopus.Diagnostics;
using Octopus.Manager.Tentacle.Dialogs;

namespace Octopus.Manager.Tentacle.Infrastructure
{
    public static class UnhandledErrorTrapper
    {
        static readonly ILog Log = Shared.Diagnostics.SystemLog.Instance;

        public static void Initialize()
        {
            TaskScheduler.UnobservedTaskException += HandleTaskException;
            AppDomain.CurrentDomain.UnhandledException += HandleAppDomainException;
            Application.Current.DispatcherUnhandledException += HandleDispatcherException;
        }

        static void HandleDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                e.Handled = true;

                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(delegate
                {
                    Log.Error(e.Exception);
                    ErrorDialog.ShowDialog(e.Exception);
                }));
            }
            else
            {
                e.Handled = false;
                ErrorDialog.ShowDialog(e.Exception);
                Environment.Exit(e.Exception.Message.GetHashCode());
            }
        }

        static void HandleAppDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Error((Exception)e.ExceptionObject);
        }

        static void HandleTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
            Log.Error(e.Exception);
        }
    }
}