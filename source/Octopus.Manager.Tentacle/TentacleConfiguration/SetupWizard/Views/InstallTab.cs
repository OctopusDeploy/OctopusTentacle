using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Octopus.Manager.Tentacle.Dialogs;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Shared.Util;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard.Views
{
    public partial class InstallTab
    {
        public static readonly DependencyProperty SuccessMessageProperty = DependencyProperty.Register("SuccessMessage", typeof (string), typeof (InstallTab), new PropertyMetadata(null));
        public static readonly DependencyProperty ReadyMessageProperty = DependencyProperty.Register("ReadyMessage", typeof (string), typeof (InstallTab), new PropertyMetadata(null));
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof (string), typeof (InstallTab), new PropertyMetadata("Install"));
        public static readonly DependencyProperty ExecuteButtonTextProperty = DependencyProperty.Register("ExecuteButtonText", typeof (string), typeof (InstallTab), new PropertyMetadata("INSTALL"));
        readonly ICommandLineRunner commandLineRunner;
        readonly Action<bool> onScriptCompletionCallback;
        readonly IScriptableViewModel model;
        readonly TextBoxLogger logger;

        public InstallTab(IScriptableViewModel model, ICommandLineRunner commandLineRunner, ContentControl additionalContent = null, Action<bool> onScriptCompletionCallback = null)
        {
            this.commandLineRunner = commandLineRunner;
            this.onScriptCompletionCallback = onScriptCompletionCallback;
            InitializeComponent();

            DataContext = this.model = model;
            logger = new TextBoxLogger(outputLog);
            IsNextEnabled = false;

            outputLog.Loaded += (sender, args) =>
            {
                outputLog.Visibility = outputLog.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            };

            extraContent.Content = additionalContent;
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string ExecuteButtonText
        {
            get => (string)GetValue(ExecuteButtonTextProperty);
            set => SetValue(ExecuteButtonTextProperty, value);
        }

        public string ReadyMessage
        {
            get => (string)GetValue(ReadyMessageProperty);
            set => SetValue(ReadyMessageProperty, value);
        }

        public string SuccessMessage
        {
            get => (string)GetValue(SuccessMessageProperty);
            set => SetValue(SuccessMessageProperty, value);
        }

        public Func<bool> FinishOnSuccessfulExecution { get; set; }

        void StartClicked(object sender, RoutedEventArgs e)
        {
            startButton.IsEnabled = false;
            outputLog.Visibility = Visibility.Visible;
            outputLog.Clear();

            ThreadPool.QueueUserWorkItem(delegate
            {
                var success = false;
                try
                {
                    var script = model.GenerateScript();
                    success = commandLineRunner.Execute(script);
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                }
                finally
                {
                    if (!success)
                    {
                        Rollback();
                    }
                    onScriptCompletionCallback?.Invoke(success);

                    Dispatcher.Invoke(() =>
                    {
                        var finished = success && (FinishOnSuccessfulExecution == null || FinishOnSuccessfulExecution());

                        IsNextEnabled = finished;
                        startButton.IsEnabled = !finished;

                        if (finished)
                        {
                            readyMessage.Visibility = Visibility.Collapsed;
                            successMessage.Visibility = Visibility.Visible;
                        }
                    });
                }
            });
        }

        void Rollback()
        {
            try
            {
                var script = model.GenerateRollbackScript();
                commandLineRunner.Execute(script);
            }
            catch (Exception ex2)
            {
                logger.Error(ex2);
            }
        }

        void GenerateScriptClicked(object sender, RoutedEventArgs e)
        {
            var script = model.GenerateScript();

            ViewScriptDialog.ShowDialog(Window.GetWindow(this), string.Join(Environment.NewLine, script.Select(s => s.ToString())));
        }
    }
}