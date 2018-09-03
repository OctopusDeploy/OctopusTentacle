using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Octopus.Manager.Tentacle.Util;

namespace Octopus.Manager.Tentacle.Controls
{
    /// <summary>
    /// Interaction logic for AutoCompleteTagControl.xaml
    /// </summary>
    public partial class AutoCompleteTagControl : UserControl
    {
        public ICommand RemoveCommand { get; }
        public ICommand EnterCommand { get; }

        public List<string> SuggestedTags
        {
            get => (List<string>)GetValue(SuggestedTagsProperty);
            set => SetValue(SuggestedTagsProperty, value);
        }

        public static readonly DependencyProperty SuggestedTagsProperty = DependencyProperty.Register("SuggestedTags",
            typeof(List<string>), typeof(AutoCompleteTagControl), new PropertyMetadata(new List<string>(), PropertyChangedCallback));

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

        public CollectionViewSource FilteredSuggestedTags { get; }

        public AutoCompleteTagControl()
        {
            RemoveCommand = new RelayCommand<string>(ExecuteRemoveCommand);
            EnterCommand = new RelayCommand<string>(ExecuteEnterCommand);
            FilteredSuggestedTags = new CollectionViewSource { Source = SuggestedTags };
            FilteredSuggestedTags.Filter += FilteredSuggestedTagsOnFilter;
            InitializeComponent();
        }

        private void UpdateFilteredSource()
        {
            FilteredSuggestedTags.Source = SuggestedTags;
        }

        private void FilteredSuggestedTagsOnFilter(object sender, FilterEventArgs filterEventArgs)
        {
            var item = filterEventArgs.Item as string;
            if (item == null)
            {
                filterEventArgs.Accepted = false;
                return;
            }

            if (SelectedTags.Contains(item, StringComparison.CurrentCultureIgnoreCase))
            {
                filterEventArgs.Accepted = false;
                return;
            }

            if (item.Contains(Text, StringComparison.CurrentCultureIgnoreCase))
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

        private void SuggestionsPopup_OnGotFocus(object sender, RoutedEventArgs e)
        {
            SuggestionsPopup.IsOpen = true;
        }

        private void TextBoxBase_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            FilteredSuggestedTags.View.Refresh();
            ValidateText();
            if (string.IsNullOrEmpty(Text))
            {
                SuggestionsPopup.IsOpen = false;
            }
            else
            {
                SuggestionsPopup.IsOpen = !FilteredSuggestedTags.View.IsEmpty;
            }

        }

        private void ValidateText()
        {
            if (string.IsNullOrEmpty(Text))
            {
                ValidationError.Visibility = Visibility.Collapsed;
                return;
            }

            ValidationError.Visibility = SelectedTags.Contains(Text, StringComparison.CurrentCultureIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void EventSetter_OnHandler(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListViewItem item)
            {
                ExecuteEnterCommand(item.Content as string);
            }
        }

        private void EventSetter_OnHandler(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            if (sender is ListViewItem item)
            {
                ExecuteEnterCommand(item.Content as string);
            }
        }

        private void TextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                SuggestionsList.SelectedIndex = 1;
                SuggestionsList.Focus();
            }
        }
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
