using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Octopus.Manager.Tentacle.Dialogs;
using Octopus.Manager.Tentacle.Infrastructure;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard.Views
{
    public partial class ReviewAndRunScriptTabView
    {
        public static readonly DependencyProperty SuccessMessageProperty = DependencyProperty.Register("SuccessMessage", typeof(string), typeof(ReviewAndRunScriptTabView), new PropertyMetadata(null));
        public static readonly DependencyProperty ReadyMessageProperty = DependencyProperty.Register("ReadyMessage", typeof(string), typeof(ReviewAndRunScriptTabView), new PropertyMetadata(null));
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(ReviewAndRunScriptTabView), new PropertyMetadata("Install"));
        public static readonly DependencyProperty ExecuteButtonTextProperty = DependencyProperty.Register("ExecuteButtonText", typeof(string), typeof(ReviewAndRunScriptTabView), new PropertyMetadata("INSTALL"));
        readonly ReviewAndRunScriptTabViewModel viewModel;

        public ReviewAndRunScriptTabView(ReviewAndRunScriptTabViewModel viewModel, ContentControl additionalContent = null)
        {
            InitializeComponent();
            var logger = new TextBoxLogger(outputLog);
            viewModel.SetLogger(logger);
            IsNextEnabled = false;
            DataContext = this.viewModel = viewModel;

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

        async void StartClicked(object sender, RoutedEventArgs e)
        {
            startButton.IsEnabled = false;
            outputLog.Visibility = Visibility.Visible;
            outputLog.Clear();
            
            bool success = await Task.Run(viewModel.GenerateAndExecuteScript);

            var finished = success && (FinishOnSuccessfulExecution == null || FinishOnSuccessfulExecution());
            IsNextEnabled = finished;
            startButton.IsEnabled = !finished;
            if (!finished) return;
            readyMessage.Visibility = Visibility.Collapsed;
            successMessage.Visibility = Visibility.Visible;
        }

        void GenerateScriptClicked(object sender, RoutedEventArgs e)
        {
            var script = viewModel.GenerateScript();
            ViewScriptDialog.ShowDialog(Window.GetWindow(this), string.Join(Environment.NewLine, script.Select(s => s.ToString())));
        }
    }
}
