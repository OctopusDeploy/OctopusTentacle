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
        public ServerConnectionDialog(TentacleSetupWizardModel model)
        {
            InitializeComponent();

            DataContext = model;
            var logger = new TextBoxLogger(outputLog);
            Loaded += async (a, e) =>
            {
                model.ContributeSensitiveValues(logger);
                await model.VerifyCredentials(logger, model.CancellationToken);
                if (model.HaveCredentialsBeenVerified)
                {
                    SetProgressBarToStatus(false, true);
                    CloseButton.Visibility = Visibility.Collapsed;
                    NextButton.Visibility = Visibility.Visible;
                    //DialogHost.CloseDialogCommand.Execute(null, null);
                }
                else
                {
                    SetProgressBarToStatus(true, true);
                }
            };
        }

        void SetProgressBarToStatus(bool error, bool isComplete)
        {
            progressBar.Value = (error || isComplete) ? 100 : 0;
            progressBar.IsIndeterminate = (!error && !isComplete);
            progressBar.Foreground = new SolidColorBrush(error ? Colors.Red : Color.FromRgb(25, 118, 210));
        }
    }
}
