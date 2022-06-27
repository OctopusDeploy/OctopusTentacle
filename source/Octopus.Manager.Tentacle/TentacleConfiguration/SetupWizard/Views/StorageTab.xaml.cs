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
        readonly TentacleSetupWizardModel model;

        public StorageTab(TentacleSetupWizardModel model)
        {
            InitializeComponent();

            DataContext = this.model = model;
        }

        void BrowseHomeDirButtonClicked(object sender, RoutedEventArgs e)
        {
            DoBrowse("Select a Tentacle home directory", model.HomeDirectory, s => model.HomeDirectory = s);
        }

        void BrowseAppDirButtonClicked(object sender, RoutedEventArgs e)
        {
            DoBrowse("Select where Tentacle should install applications", model.ApplicationInstallDirectory, s => model.ApplicationInstallDirectory = s);
        }

        void DoBrowse(string description, string currentDirectory, Action<string> store)
        {
            var currentDir = Environment.CurrentDirectory;
            var folderBrowser = new FolderBrowserDialog();
            folderBrowser.ShowNewFolderButton = true;
            folderBrowser.Description = description;
            if (Directory.Exists(currentDirectory))
            {
                folderBrowser.SelectedPath = model.HomeDirectory;
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