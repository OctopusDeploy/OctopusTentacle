using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard;

namespace Octopus.Manager.Tentacle.Dialogs
{
    /// <summary>
    /// Interaction logic for ServerConnectionDialog.xaml
    /// </summary>
    public partial class ServerConnectionDialog : UserControl
    {
        private readonly TentacleSetupWizardModel model;
        private readonly TextBoxLogger logger;

        public ServerConnectionDialog(TentacleSetupWizardModel model)
        {
            InitializeComponent();

            DataContext = this.model = model;
            logger = new TextBoxLogger(outputLog);
            Loaded += async (a, e) =>
            {
                model.ContributeSensitiveValues(logger);
                await model.VerifyCredentials(logger);
                if (model.HaveCredentialsBeenVerified)
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

        private void setProgressBarToStatus(bool error, bool isComplete)
        {
            progressBar.Value = error || isComplete ? 100 : 0;
            progressBar.IsIndeterminate = !error && !isComplete;
            progressBar.Foreground = new SolidColorBrush(error ? Colors.Red : Color.FromRgb(25, 118, 210));
        }
    }
}