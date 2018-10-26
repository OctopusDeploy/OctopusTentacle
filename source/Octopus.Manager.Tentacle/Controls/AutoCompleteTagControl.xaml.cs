using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Octopus.Manager.Tentacle.Util;

namespace Octopus.Manager.Tentacle.Controls
{
    internal class CustomIconDataTemplateSelector : DataTemplateSelector
    {
        #region Data Templates
        static readonly DataTemplate RoleIconDataTemplate = Application.Current.Resources["RoleIconDataTemplate"] as DataTemplate;
        static readonly DataTemplate EnvironmentIconDataTemplate = Application.Current.Resources["EnvironmentIconDataTemplate"] as DataTemplate;
        static readonly DataTemplate TenantIconDataTemplate = Application.Current.Resources["TenantIconDataTemplate"] as DataTemplate;
        static readonly DataTemplate WorkerPoolIconDataTemplate = Application.Current.Resources["WorkerPoolIconDataTemplate"] as DataTemplate;
        #endregion

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
                    return TenantIconDataTemplate;
                case "worker pools":
                    return WorkerPoolIconDataTemplate;
                default:
                    return RoleIconDataTemplate;
            }
        }
    }

    internal class CustomDataTemplateSelector : DataTemplateSelector
    {
        #region Data Templates
        static readonly DataTemplate CreateATagTemplate = Application.Current.Resources["CreateATagTemplate"] as DataTemplate;
        static readonly DataTemplate SuggestedTagTemplate = Application.Current.Resources["SuggestedTagTemplate"] as DataTemplate;

        #endregion

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
    }

    /// <summary>
    /// Interaction logic for AutoCompleteTagControl.xaml
    /// </summary>
    public partial class AutoCompleteTagControl : UserControl
    {
        public ICommand RemoveCommand { get; }
        public ICommand EnterCommand { get; }

        public IEnumerable<string> SuggestedTags
        {
            get => (IEnumerable<string>)GetValue(SuggestedTagsProperty);
            set => SetValue(SuggestedTagsProperty, value);
        }

        public static readonly DependencyProperty SuggestedTagsProperty = DependencyProperty.Register("SuggestedTags",
            typeof(IEnumerable<string>), typeof(AutoCompleteTagControl), new PropertyMetadata(new List<string>(), PropertyChangedCallback));

        private static void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as AutoCompleteTagControl)?.UpdateFilteredSource();
        }

        public ObservableCollection<string> SelectedTags
        {
            get => (ObservableCollection<string>)GetValue(SelectedTagsProperty);
            set => SetValue(SelectedTagsProperty, value);
        }

        public static readonly DependencyProperty SelectedTagsProperty = DependencyProperty.Register("SelectedTags",
            typeof(ObservableCollection<string>), typeof(AutoCompleteTagControl), new PropertyMetadata(new ObservableCollection<string>()));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            "Text", typeof(string), typeof(AutoCompleteTagControl), new PropertyMetadata(string.Empty));


        public string TagName
        {
            get => (string)GetValue(TagNameProperty);
            set => SetValue(TagNameProperty, value);
        }

        public static readonly DependencyProperty TagNameProperty = DependencyProperty.Register(
            "TagName", typeof(string), typeof(AutoCompleteTagControl), new PropertyMetadata("tag"));

        public bool CanCreateNewTags { get; set; }

        public string Watermark => CanCreateNewTags ? $"{TagName.FirstCharToUpper()} (type to add a new {TagName})" : $"Select {TagName.ToLower()}";

        public CollectionViewSource FilteredSuggestedTags { get; }

        List<SuggestedTagContainer> InternalSuggestedTags  = new List<SuggestedTagContainer>();

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


        void ViewOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
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
            if(CanCreateNewTags)
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
                filterEventArgs.Accepted = !string.IsNullOrWhiteSpace(Text) && !SuggestedTags.Contains(Text, StringComparison.InvariantCultureIgnoreCase) && !SelectedTags.Contains(Text, StringComparison.InvariantCultureIgnoreCase);
                return;
            }

            if (SelectedTags.Contains(item.Value, StringComparison.CurrentCultureIgnoreCase))
            {
                filterEventArgs.Accepted = false;
                return;
            }

            if (item.Value.Contains(Text, StringComparison.CurrentCultureIgnoreCase))
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
        }

        private bool AddTag(string value)
        {
            if (SelectedTags.Contains(value, StringComparison.CurrentCultureIgnoreCase)) return false;

            var matchingSuggested = SuggestedTags.Find(value, StringComparison.CurrentCultureIgnoreCase);

            SelectedTags.Add(matchingSuggested ?? value);
            return true;

        }

        private void TextBox_OnGotFocus(object sender, RoutedEventArgs e)
        {
            SuggestionsPopup.IsOpen = true;
        }

        void TextBox_OnLostFocus(object sender, RoutedEventArgs e)
        {
            if(!SuggestionsPopup.IsKeyboardFocusWithin && !TextBox.IsKeyboardFocusWithin)
                SuggestionsPopup.IsOpen = false;
        }

        void SuggestionsList_OnLostFocus(object sender, RoutedEventArgs e)
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
            {
                if (item.DataContext is SuggestedTagContainer container)
                {
                    ExecuteEnterCommand(container.IsCreateEntry ? Text : container.Value);
                }
            }
        }

        private void EventSetter_OnHandler(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            if (sender is ListViewItem item)
            {
                if (item.DataContext is SuggestedTagContainer container)
                {
                    e.Handled = true;
                    ExecuteEnterCommand(container.IsCreateEntry ? Text : container.Value);
                }
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

        void TextBox_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            SuggestionsPopup.IsOpen = true;
        }
    }

    internal class SuggestedTagContainer
    {
        public bool IsCreateEntry { get; }

        public string Value { get; }

        public SuggestedTagContainer(string value, bool isCreateEntry = false)
        {
            IsCreateEntry = isCreateEntry;
            Value = value;
        }

    }

    public static class ExtensionMethods
    {
        public static string FirstCharToUpper(this string input)
        {
            if (!string.IsNullOrEmpty(input))
                return input.First().ToString().ToUpper() + input.Substring(1);
            return input;
        }

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
