using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Shared.Util;

namespace Octopus.Manager.Tentacle.Dialogs
{
    public partial class RunProcessDialog : Window
    {
        readonly IList<CommandLineInvocation> commandLines;
        readonly string logsDirectory;
        readonly ICommandLineRunner commandLineRunner;
        readonly TextBoxLogger logger;

        public RunProcessDialog(IList<CommandLineInvocation> commandLines, string logsDirectory)
        {
            this.commandLines = commandLines;
            this.logsDirectory = logsDirectory;
            InitializeComponent();
            logger = new TextBoxLogger(OutputLog);
            commandLineRunner = new CommandLineRunner();
        }

        public static void ShowDialog(Window owner, IEnumerable<CommandLineInvocation> commandLines, string title, string logsDirectory, bool showOutputLog = false)
        {
            var dialog = new RunProcessDialog(commandLines.ToList(), logsDirectory)
            {
                Title = title,
                Owner = owner
            };
            if (dialog.Owner == null)
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            dialog.Run(showOutputLog);
            dialog.ShowDialog();
        }

        void Run(bool showOutputLog)
        {
            OutputLog.Visibility = Visibility.Collapsed;
            StatusProgressBar.Visibility = Visibility.Visible;
            AutoClose.Visibility = Visibility.Visible;
            OutputLog.Clear();

            if (showOutputLog) ShowOutputLog();

            ThreadPool.QueueUserWorkItem(delegate
            {
                var success = false;
                try
                {
                    success = commandLineRunner.Execute(commandLines, logger);
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                }
                finally
                {
                    Dispatcher.Invoke<Task>(async () =>
                    {
                        SetStatusBarCompleted(success);

                        if (success)
                        {
                            if (OutputLog.Visibility == Visibility.Visible)
                            {
                                // Provide a small chance to see the end result if the output log is visible
                                await Task.Delay(2000);
                            }

                            if (AutoClose.IsChecked == true)
                            {
                                Close();
                            }
                            else
                            {
                                AutoClose.Visibility = Visibility.Collapsed;
                                LogsLink.Visibility = Visibility.Visible;
                                CloseButton.Visibility = Visibility.Visible;
                            }
                        }
                        else
                        {
                            // Show the user what went wrong
                            ShowOutputLog();
                            AutoClose.Visibility = Visibility.Collapsed;
                            LogsLink.Visibility = Visibility.Visible;
                            CloseButton.Visibility = Visibility.Visible;
                        }
                    });
                }
            });
        }

        void SetStatusBarCompleted(bool success)
        {
            StatusProgressBar.IsIndeterminate = false;
            StatusProgressBar.Value = 100;
            if (!success)
            {
                StatusProgressBar.Foreground = Brushes.Red;
            }
        }

        void ShowOutputLog()
        {
            OutputLog.Visibility = Visibility.Visible;
            Width = 600;
            Height = 400;
            ResizeMode = ResizeMode.CanResizeWithGrip;
        }

        void LogsHyperlink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(logsDirectory);
        }
    }
}