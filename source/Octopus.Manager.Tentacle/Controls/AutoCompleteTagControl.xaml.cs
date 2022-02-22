using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Octopus.Manager.Tentacle.Annotations;
using Octopus.Manager.Tentacle.Util;

namespace Octopus.Manager.Tentacle.Controls
{
    internal class CustomIconDataTemplateSelector : DataTemplateSelector
    {
        /// <summary>
        /// When overridden in a derived class, returns a <see cref="T:System.Windows.DataTemplate" /> based on custom logic.
        /// </summary>
        /// <param name="item">The data object for which to select the template.</param>
        /// <param name="container">The data-bound object.</param>
        /// <returns>
        /// Returns a <see cref="T:System.Windows.DataTemplate" /> or null. The default value is null.
        /// </returns>
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var tagName = (string)item;
            if (string.IsNullOrEmpty(tagName)) return null;
            switch (tagName.ToLowerInvariant())
            {
                case "roles":
                    return RoleIconDataTemplate;
                case "environments":
                    return EnvironmentIconDataTemplate;
                case "tenants":
                case "tenant tags":
                    return TenantIconDataTemplate;
                case "worker pools":
                    return WorkerPoolIconDataTemplate;
                default:
                    return RoleIconDataTemplate;
            }
        }

        #region Data Templates

        private static readonly DataTemplate RoleIconDataTemplate = Application.Current.Resources["RoleIconDataTemplate"] as DataTemplate;
        private static readonly DataTemplate EnvironmentIconDataTemplate = Application.Current.Resources["EnvironmentIconDataTemplate"] as DataTemplate;
        private static readonly DataTemplate TenantIconDataTemplate = Application.Current.Resources["TenantIconDataTemplate"] as DataTemplate;
        private static readonly DataTemplate WorkerPoolIconDataTemplate = Application.Current.Resources["WorkerPoolIconDataTemplate"] as DataTemplate;

        #endregion
    }

    internal class CustomDataTemplateSelector : DataTemplateSelector
    {
        /// <summary>
        /// When overridden in a derived class, returns a <see cref="T:System.Windows.DataTemplate" /> based on custom logic.
        /// </summary>
        /// <param name="item">The data object for which to select the template.</param>
        /// <param name="container">The data-bound object.</param>
        /// <returns>
        /// Returns a <see cref="T:System.Windows.DataTemplate" /> or null. The default value is null.
        /// </returns>
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var tagContainer = (SuggestedTagContainer)item;
            if (tagContainer == null) return null;
            return tagContainer.IsCreateEntry ? CreateATagTemplate : SuggestedTagTemplate;
        }

        #region Data Templates

        private static readonly DataTemplate CreateATagTemplate = Application.Current.Resources["CreateATagTemplate"] as DataTemplate;
        private static readonly DataTemplate SuggestedTagTemplate = Application.Current.Resources["SuggestedTagTemplate"] as DataTemplate;

        #endregion
    }

    /// <summary>
    /// Interaction logic for AutoCompleteTagControl.xaml
    /// </summary>
    public partial class AutoCompleteTagControl : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty SuggestedTagsProperty = DependencyProperty.Register("SuggestedTags",
            typeof(IEnumerable<string>), typeof(AutoCompleteTagControl), new PropertyMetadata(new List<string>(), PropertyChangedCallback));

        public static readonly DependencyProperty SelectedTagsProperty = DependencyProperty.Register("SelectedTags",
            typeof(ObservableCollection<string>), typeof(AutoCompleteTagControl), new PropertyMetadata(new ObservableCollection<string>()));

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            "Text", typeof(string), typeof(AutoCompleteTagControl), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty TagNameProperty = DependencyProperty.Register(
            "TagName", typeof(string), typeof(AutoCompleteTagControl), new PropertyMetadata("tag", TagPropertyChangedCallback));

        private readonly List<SuggestedTagContainer> InternalSuggestedTags = new List<SuggestedTagContainer>();
        private bool canCreateNewTags;

        public AutoCompleteTagControl()
        {
            RemoveCommand = new RelayCommand<string>(ExecuteRemoveCommand);
            EnterCommand = new RelayCommand<string>(ExecuteEnterCommand);

            if (CanCreateNewTags)
                InternalSuggestedTags.Add(new SuggestedTagContainer(string.Empty, true));
            InternalSuggestedTags.AddRange(SuggestedTags.Select(t => new SuggestedTagContainer(t)));

            FilteredSuggestedTags = new CollectionViewSource { Source = InternalSuggestedTags };
            FilteredSuggestedTags.Filter += FilteredSuggestedTagsOnFilter;
            FilteredSuggestedTags.View.CollectionChanged += ViewOnCollectionChanged;

            InitializeComponent();
        }

        public ICommand RemoveCommand { get; }
        public ICommand EnterCommand { get; }

        public IEnumerable<string> SuggestedTags
        {
            get => (IEnumerable<string>)GetValue(SuggestedTagsProperty);
            set => SetValue(SuggestedTagsProperty, value);
        }

        public ObservableCollection<string> SelectedTags
        {
            get => (ObservableCollection<string>)GetValue(SelectedTagsProperty);
            set => SetValue(SelectedTagsProperty, value);
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string TagName
        {
            get => (string)GetValue(TagNameProperty);
            set => SetValue(TagNameProperty, value);
        }

        public bool CanCreateNewTags
        {
            get => canCreateNewTags;
            set
            {
                if (value == canCreateNewTags) return;
                canCreateNewTags = value;
                OnPropertyChanged(nameof(Watermark));
            }
        }

        public string Watermark => CanCreateNewTags ? $"{TagName.FirstCharToUpper()} (type to add new {TagName})" : $"Select {TagName.ToLower()}";

        public CollectionViewSource FilteredSuggestedTags { get; }

        private static void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as AutoCompleteTagControl)?.UpdateFilteredSource();
        }

        private static void TagPropertyChangedCallback(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is AutoCompleteTagControl c) c.OnAutoCompleteTagControl();
        }

        protected virtual void OnAutoCompleteTagControl()
        {
            OnPropertyChanged(nameof(Watermark));
        }

        private void ViewOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (FilteredSuggestedTags.View.IsEmpty)
            {
                NoResultsTest.Visibility = Visibility.Visible;
                SuggestionsList.Visibility = Visibility.Collapsed;
            }
            else
            {
                NoResultsTest.Visibility = Visibility.Collapsed;
                SuggestionsList.Visibility = Visibility.Visible;
            }
        }

        private void UpdateFilteredSource()
        {
            InternalSuggestedTags.Clear();
            if (CanCreateNewTags)
                InternalSuggestedTags.Add(new SuggestedTagContainer(string.Empty, true));

            if (SuggestedTags != null)
                InternalSuggestedTags.AddRange(SuggestedTags.Select(t => new SuggestedTagContainer(t)));

            SelectedTags.CollectionChanged += (sender, args) => FilteredSuggestedTags.View.Refresh();

            FilteredSuggestedTags.View.Refresh();
        }

        private void FilteredSuggestedTagsOnFilter(object sender, FilterEventArgs filterEventArgs)
        {
            var item = filterEventArgs.Item as SuggestedTagContainer;
            if (item == null)
            {
                filterEventArgs.Accepted = false;
                return;
            }

            if (item.IsCreateEntry)
            {
                filterEventArgs.Accepted = !string.IsNullOrWhiteSpace(Text) && !SuggestedTags.Contains(Text, StringComparison.OrdinalIgnoreCase) && !SelectedTags.Contains(Text, StringComparison.OrdinalIgnoreCase);
                return;
            }

            if (SelectedTags.Contains(item.Value, StringComparison.OrdinalIgnoreCase))
            {
                filterEventArgs.Accepted = false;
                return;
            }

            if (item.Value.Contains(Text, StringComparison.OrdinalIgnoreCase))
            {
                filterEventArgs.Accepted = true;
                return;
            }

            filterEventArgs.Accepted = false;
        }

        private void ExecuteEnterCommand(string stringValue)
        {
            if (!string.IsNullOrWhiteSpace(stringValue) && AddTag(stringValue))
            {
                TextBox.Text = string.Empty;
                SuggestionsPopup.IsOpen = false;
            }
        }

        private void ExecuteRemoveCommand(string stringValue)
        {
            SelectedTags.Remove(stringValue);
            if (SelectedTags.Count == 0)
                Label.Visibility = Visibility.Hidden;
        }

        private bool AddTag(string value)
        {
            if (SelectedTags.Contains(value, StringComparison.OrdinalIgnoreCase)) return false;

            var matchingSuggested = SuggestedTags.Find(value, StringComparison.OrdinalIgnoreCase);

            SelectedTags.Add(matchingSuggested ?? value);
            return true;
        }

        private void TextBox_OnGotFocus(object sender, RoutedEventArgs e)
        {
            Label.Visibility = Visibility.Visible;
            SuggestionsPopup.IsOpen = true;
        }

        private void TextBox_OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (!SuggestionsPopup.IsKeyboardFocusWithin && !TextBox.IsKeyboardFocusWithin)
            {
                Label.Visibility = SelectedTags.Count > 0 ? Visibility.Visible : Visibility.Hidden;
                SuggestionsPopup.IsOpen = false;
            }
        }

        private void SuggestionsList_OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (!SuggestionsPopup.IsKeyboardFocusWithin && !TextBox.IsKeyboardFocusWithin)
                SuggestionsPopup.IsOpen = false;
        }

        private void TextBoxBase_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            FilteredSuggestedTags.View.Refresh();
        }

        private void EventSetter_OnHandler(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListViewItem item)
                if (item.DataContext is SuggestedTagContainer container)
                    ExecuteEnterCommand(container.IsCreateEntry ? Text : container.Value);
        }

        private void EventSetter_OnHandler(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            if (sender is ListViewItem item)
                if (item.DataContext is SuggestedTagContainer container)
                {
                    e.Handled = true;
                    ExecuteEnterCommand(container.IsCreateEntry ? Text : container.Value);
                }
        }

        private void TextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) e.Handled = true;
            if (e.Key == Key.Down)
            {
                SuggestionsList.SelectedIndex = 0;
                SuggestionsList.Focus();
            }
        }

        private void TextBox_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            SuggestionsPopup.IsOpen = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    internal class SuggestedTagContainer
    {
        public SuggestedTagContainer(string value, bool isCreateEntry = false)
        {
            IsCreateEntry = isCreateEntry;
            Value = value;
        }

        public bool IsCreateEntry { get; }

        public string Value { get; }
    }

    public static class ExtensionMethods
    {
        public static bool Contains(this IEnumerable<string> source, string value, StringComparison comparison)
        {
            return source.ToList().FindAll(s => s.Equals(value, StringComparison.OrdinalIgnoreCase)).Count > 0;
        }

        public static string Find(this IEnumerable<string> source, string value, StringComparison comparison)
        {
            return source.ToList().FindAll(s => s.Equals(value, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        }

        public static bool Contains(this string target, string value, StringComparison comparison)
        {
            return target.IndexOf(value, comparison) >= 0;
        }
    }
}