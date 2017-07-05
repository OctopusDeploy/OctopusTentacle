using Octopus.Manager.Tentacle.Dialogs;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Octopus.Manager.Tentacle.Shell
{
    public partial class ShellView
    {
        readonly ShellViewModel viewModel;

        public ShellView(string title, ShellViewModel viewModel)
        {
            InitializeComponent();

            this.viewModel = viewModel;
            DataContext = viewModel;

            Title = title;
        }

        public void SetViewContent(ContentControl control)
        {
            mainContent.Content = control;
        }

        public void EnableInstanceSelection()
        {
            instanceSelectionContainer.Visibility = Visibility.Visible;
            viewModel.InstanceSelectionModel.Refresh();
        }

        void OnAddNewInstance(object sender, ExecutedRoutedEventArgs e)
        {
            var dialog = new NewInstanceNameDialog(viewModel.InstanceSelectionModel.Instances.Select(q => q.InstanceName));
            dialog.Owner = this;
            if (dialog.ShowDialog() ?? false)
            {
                viewModel.InstanceSelectionModel.New(dialog.InstanceName);
            }
        }
    }
}