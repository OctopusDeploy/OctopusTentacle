using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using Octopus.Manager.Tentacle.Infrastructure;

namespace Octopus.Manager.Tentacle.PreReq
{
    /// <summary>
    /// Interaction logic for PreReqWindow.xaml
    /// </summary>
    public partial class PreReqWindow : Window
    {
        private readonly IPrerequisiteProfile profile;
        private bool hasCompletedSuccessfully;

        public PreReqWindow(IPrerequisiteProfile profile)
        {
            this.profile = profile;
            InitializeComponent();

            Loaded += (sender, args) => Start();
            Closing += OnClosing;

            ExitButtonText = "EXIT";
        }

        public string ExitButtonText { get; set; }

        private void Start()
        {
            DispatchHelper.Foreground(() =>
            {
                cancelButton.Content = "CANCEL";
                reCheckButton.Visibility = Visibility.Collapsed;
                correctTextBox.Visibility = Visibility.Collapsed;
                correctLinkBlock.Visibility = Visibility.Collapsed;
            });

            DispatchHelper.Background(() =>
            {
                var failed = false;

                foreach (var prereq in profile.Prerequisites)
                {
                    DispatchHelper.Foreground(() =>
                    {
                        statusText.Text = prereq.StatusMessage;
                        progressBar.Visibility = Visibility.Visible;
                    });

                    var result = prereq.Check();

                    if (result.Success)
                        continue;

                    failed = true;

                    DispatchHelper.Foreground(() =>
                    {
                        statusText.Text = result.Message;

                        progressBar.Visibility = Visibility.Collapsed;

                        if (!string.IsNullOrWhiteSpace(result.HelpLink))
                        {
                            var click = new Hyperlink();
                            click.Click += (sender, args) => NavigateTo(result.HelpLink);
                            click.Inlines.Add(result.HelpLinkText);
                            correctLinkBlock.Inlines.Clear();
                            correctLinkBlock.Inlines.Add(click);
                            correctLinkBlock.Visibility = Visibility.Visible;
                        }

                        if (!string.IsNullOrWhiteSpace(result.CommandLineSolution))
                        {
                            correctTextBox.Text = result.CommandLineSolution;
                            correctTextBox.Visibility = Visibility.Visible;
                        }

                        commandLineOutputTextBox.Text = result.CommandLineOutput;
                        moreInfoLinkBlock.Visibility = Visibility.Visible;
                    });

                    break;
                }

                if (failed)
                {
                    DispatchHelper.Foreground(() =>
                    {
                        cancelButton.Content = ExitButtonText;
                        reCheckButton.Visibility = Visibility.Visible;
                    });
                }
                else
                {
                    hasCompletedSuccessfully = true;

                    DispatchHelper.Foreground(() =>
                    {
                        DialogResult = true;
                        Close();
                    });
                }
            });
        }

        private void OnClosing(object sender, CancelEventArgs cancelEventArgs)
        {
            if (!hasCompletedSuccessfully) DialogResult = false;
        }

        private void NavigateTo(string helpLink)
        {
            Process.Start(helpLink);
        }

        private void CancelClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CopyToClipboardClicked(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(commandLineOutputTextBox.Text);
        }

        private void ReCheckClicked(object sender, RoutedEventArgs e)
        {
            commandLineOutputTextBox.Visibility = Visibility.Collapsed;
            moreInfoLinkBlock.Visibility = Visibility.Collapsed;
            copyToClipboard.Visibility = Visibility.Collapsed;
            Start();
        }

        private void MoreInfoClicked(object sender, RoutedEventArgs e)
        {
            commandLineOutputTextBox.Visibility = Visibility.Visible;
            moreInfoLinkBlock.Visibility = Visibility.Collapsed;
            copyToClipboard.Visibility = Visibility.Visible;
        }
    }
}