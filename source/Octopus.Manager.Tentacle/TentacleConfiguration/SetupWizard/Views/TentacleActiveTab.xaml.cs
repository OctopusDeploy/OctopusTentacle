using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Manager.Tentacle.Util;
using Color = System.Drawing.Color;

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

        public override async Task OnNext(CancelEventArgs e)
        {
            await base.OnNext(e);
            model.PushRuleSet("TentacleActive");
            model.ProxyWizardModel.PushRuleSet("ProxySettings");
            model.Validate();
            model.ProxyWizardModel.Validate();
            if (!model.IsValid || !model.ProxyWizardModel.IsValid)
            {
                e.Cancel = true;
                return;
            }

            setProgressBarToStatus(false, false);
            connectionDialog.Visibility = Visibility.Visible;
            outputLog.Visibility = Visibility.Visible;
            logger.Clear();

            await model.VerifyCredentials(logger);
            if (model.HaveCredentialsBeenVerified)
            {
                setProgressBarToStatus(false, true);
                connectionDialog.Visibility = Visibility.Hidden;
            }
            else
            {
                setProgressBarToStatus(true, true);
                e.Cancel = true;
            }
        }

        void setProgressBarToStatus(bool error, bool isComplete)
        {
            progressBar.Value = (error || isComplete) ? 100 : 0;
            progressBar.IsIndeterminate = (!error && !isComplete);
            progressBar.Foreground = new SolidColorBrush(error ? Colors.Red : Colors.Green);
        }

        void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            connectionDialog.Visibility = Visibility.Hidden;
        }

        void ConnectionDialog_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            IsNextEnabled = ((Grid) sender).Visibility != Visibility.Visible;
            IsBackEnabled = ((Grid) sender).Visibility != Visibility.Visible;
        }
    }
}