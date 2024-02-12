using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard.Views
{
    /// <summary>
    /// Interaction logic for StorageTab.xaml
    /// </summary>
    public partial class StorageTab
    {
        readonly SetupTentacleWizardModel viewModel;

        public StorageTab(SetupTentacleWizardModel viewModel)
        {
            InitializeComponent();

            DataContext = this.viewModel = viewModel;
        }

        void BrowseHomeDirButtonClicked(object sender, RoutedEventArgs e)
        {
            DoBrowse("Select a Tentacle home directory", viewModel.HomeDirectory, s => viewModel.HomeDirectory = s);
        }

        void BrowseAppDirButtonClicked(object sender, RoutedEventArgs e)
        {
            DoBrowse("Select where Tentacle should install applications", viewModel.ApplicationInstallDirectory, s => viewModel.ApplicationInstallDirectory = s);
        }

        void DoBrowse(string description, string currentDirectory, Action<string> store)
        {
            var currentDir = Environment.CurrentDirectory;
            var folderBrowser = new FolderBrowserDialog();
            folderBrowser.ShowNewFolderButton = true;
            folderBrowser.Description = description;
            if (Directory.Exists(currentDirectory))
            {
                folderBrowser.SelectedPath = viewModel.HomeDirectory;
            }
            else
            {
                folderBrowser.RootFolder = Environment.SpecialFolder.MyComputer;
            }

            var result = folderBrowser.ShowDialog();
            if (result == DialogResult.Cancel)
            {
                return;
            }

            store(folderBrowser.SelectedPath);

            Environment.CurrentDirectory = currentDir;
        }
    }
}