using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Octopus.Manager.Tentacle.Dialogs;
using Octopus.Manager.Tentacle.Infrastructure;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard.Views
{
    public partial class InstallationTabView
    {
        public static readonly DependencyProperty SuccessMessageProperty = DependencyProperty.Register("SuccessMessage", typeof(string), typeof(InstallationTabView), new PropertyMetadata(null));
        public static readonly DependencyProperty ReadyMessageProperty = DependencyProperty.Register("ReadyMessage", typeof(string), typeof(InstallationTabView), new PropertyMetadata(null));
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(InstallationTabView), new PropertyMetadata("Install"));
        public static readonly DependencyProperty ExecuteButtonTextProperty = DependencyProperty.Register("ExecuteButtonText", typeof(string), typeof(InstallationTabView), new PropertyMetadata("INSTALL"));
        readonly InstallationTabViewModel model;

        public InstallationTabView(InstallationTabViewModel model, ContentControl additionalContent = null)
        {
            InitializeComponent();
            var logger = new TextBoxLogger(outputLog);
            model.SetLogger(logger);
            IsNextEnabled = false;
            DataContext = this.model = model;

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
            var success = false;
            ThreadPool.QueueUserWorkItem(delegate
            {
                success = model.GenerateAndExecuteScript();
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
            });
        }
        
        void GenerateScriptClicked(object sender, RoutedEventArgs e)
        {
            var script = model.GenerateScript();
            ViewScriptDialog.ShowDialog(Window.GetWindow(this), string.Join(Environment.NewLine, script.Select(s => s.ToString())));
        }
    }
}