using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MaterialDesignThemes.Wpf;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard;

namespace Octopus.Manager.Tentacle.Dialogs
{
    /// <summary>
    /// Interaction logic for ServerConnectionDialog.xaml
    /// </summary>
    public partial class ServerConnectionDialog : UserControl
    {
        readonly SetupWizardViewModel viewModel;
        readonly TextBoxLogger logger;

        public ServerConnectionDialog(SetupWizardViewModel viewModel)
        {
            InitializeComponent();

            DataContext = this.viewModel = viewModel;
            logger = new TextBoxLogger(outputLog);
            Loaded += async (a, e) =>
            {
                viewModel.ContributeSensitiveValues(logger);
                await viewModel.VerifyCredentials(logger);
                if (viewModel.HaveCredentialsBeenVerified)
                {
                    setProgressBarToStatus(false, true);
                    CloseButton.Visibility = Visibility.Collapsed;
                    NextButton.Visibility = Visibility.Visible;
                    //DialogHost.CloseDialogCommand.Execute(null, null);
                }
                else
                {
                    setProgressBarToStatus(true, true);
                }
            };
        }

        void setProgressBarToStatus(bool error, bool isComplete)
        {
            progressBar.Value = (error || isComplete) ? 100 : 0;
            progressBar.IsIndeterminate = (!error && !isComplete);
            progressBar.Foreground = new SolidColorBrush(error ? Colors.Red : Color.FromRgb(25, 118, 210));
        }
    }
}
