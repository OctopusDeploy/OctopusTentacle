using System.Windows;
using System.Windows.Navigation;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Manager.Tentacle.Util;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard.Views
{
    /// <summary>
    /// Interaction logic for TentacleActiveTab.xaml
    /// </summary>
    public partial class TentacleActiveTab
    {
        readonly TentacleSetupWizardModel model;
        readonly TextBoxLogger logger;

        public TentacleActiveTab(TentacleSetupWizardModel model)
        {
            InitializeComponent();

            DataContext = this.model = model;
            logger = new TextBoxLogger(outputLog);
            Loaded += (a, e) =>
            {
                outputLog.Visibility = outputLog.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            };
        }

        void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            model.Password = PasswordTextBox.Password;
        }

        void Navigate(object sender, RequestNavigateEventArgs e)
        {
            BrowserHelper.Open(e.Uri);
            e.Handled = true;
        }

        async void AuthenticateClicked(object sender, RoutedEventArgs e)
        {
            model.PushRuleSet("TentacleActive");
            model.Validate();
            if (!model.IsValid)
                return;

            authenticateButton.IsEnabled = false;
            outputLog.Visibility = Visibility.Visible;
            logger.Clear();

            await model.VerifyCredentials(logger);
            authenticateButton.IsEnabled = true;
        }
    }
}