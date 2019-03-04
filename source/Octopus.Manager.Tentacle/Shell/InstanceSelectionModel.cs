using System;
using System.Collections.ObjectModel;
using System.Linq;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Shared.Configuration;
using Octopus.Shared.Util;

namespace Octopus.Manager.Tentacle.Shell
{
    public class InstanceSelectionModel : ViewModel
    {
        readonly IApplicationInstanceStore store;
        string selectedInstance;

        public InstanceSelectionModel(ApplicationName applicationName, IApplicationInstanceStore store)
        {
            this.ApplicationName = applicationName;
            this.store = store;
        }

        public ObservableCollection<Instance> Instances { get; } = new ObservableCollection<Instance>();

        public string SelectedInstance
        {
            get => selectedInstance;
            set
            {
                if (value == selectedInstance) return;
                selectedInstance = value;
                OnPropertyChanged();
                OnSelectionChanged();
            }
        }

        public ApplicationName ApplicationName { get; }

        public event Action SelectionChanged;

        protected virtual void OnSelectionChanged()
        {
            SelectionChanged?.Invoke();
        }

        public void Refresh()
        {
            var currentlySelectedInstance = selectedInstance;
            var existing = store.ListInstances(ApplicationName);

            Instances.Clear();
            if (existing.Count > 0)
            {
                var defaultInstance = existing.FirstOrDefault(e => e.InstanceName == ApplicationInstanceRecord.GetDefaultInstance(ApplicationName));
                var nonDefault = existing.Except(new[] {defaultInstance});

                if (defaultInstance != null)
                {
                    Instances.Add(new Instance {DisplayName = "(Default)", InstanceName = defaultInstance.InstanceName});
                }

                Instances.AddRange(nonDefault.Select(e => new Instance {DisplayName = e.InstanceName, InstanceName = e.InstanceName}).OrderBy(o => o.InstanceName));
            }

            if (Instances.Count == 0)
            {
                Instances.Add(new Instance {InstanceName = ApplicationInstanceRecord.GetDefaultInstance(ApplicationName), DisplayName = "(Default)"});
            }

            if (Instances.Count <= 0) return;

            if (string.IsNullOrWhiteSpace(currentlySelectedInstance))
            {
                SelectedInstance = Instances.First().InstanceName;
            }
            else if (Instances.All(a => a.InstanceName != currentlySelectedInstance))
            {
                SelectedInstance = Instances.First().InstanceName;
            }
            else
            {
                SelectedInstance = currentlySelectedInstance;
            }
        }

        public void New(string instanceName)
        {
            Instances.Add(new Instance {DisplayName = instanceName, InstanceName = instanceName});
            SelectedInstance = instanceName;
        }

        public class Instance
        {
            public string DisplayName { get; set; }
            public string InstanceName { get; set; }
            public bool IsNewItem { get; set; }
        }
    }
}