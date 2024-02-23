using System;
using Octopus.Manager.Tentacle.Dialogs;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace Octopus.Manager.Tentacle.Shell
{
    public partial class ShellView
    {
        readonly ShellViewModel viewModel;

        public ShellView(string title, ShellViewModel viewModel)
        {
            InitializeComponent();

#pragma warning disable CA1416
            DialogHost.Identifier = title;
#pragma warning restore CA1416

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

        async void OnAddNewInstance(object sender, ExecutedRoutedEventArgs e)
        {
#pragma warning disable CA1416
            var result = await DialogHost.Show(new NewInstanceNameDialog(viewModel.InstanceSelectionModel.Instances.Select(q => q.InstanceName)), Title);
#pragma warning restore CA1416
            /*
            var dialog = new NewInstanceNameDialog(viewModel.InstanceSelectionModel.Instances.Select(q => q.InstanceName));
            dialog.Owner = this;
            */
            if (result is string typedResult)
            {
                viewModel.InstanceSelectionModel.New(typedResult);
            }
        }
    }
}
