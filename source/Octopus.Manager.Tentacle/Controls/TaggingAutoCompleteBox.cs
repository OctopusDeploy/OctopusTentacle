using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Octopus.Manager.Tentacle.Controls
{
    public class TaggingAutoCompleteBox : AutoCompleteBox
    {
        public static readonly DependencyProperty TagsSourceProperty = DependencyProperty.Register("TagsSource", typeof(object), typeof(TaggingAutoCompleteBox), new PropertyMetadata(null));

        public TaggingAutoCompleteBox()
        {
            Populating += HandlePopulating;
            FilterMode = AutoCompleteFilterMode.Custom;
            ItemFilter = (search, item) => true;
        }

        public object TagsSource
        {
            get => GetValue(TagsSourceProperty);
            set => SetValue(TagsSourceProperty, value);
        }

        public TextBox TextBox { get; set; }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            TextBox = Template.FindName("Text", this) as TextBox;
        }

        private void HandlePopulating(object sender, PopulatingEventArgs e)
        {
            var possibleTags = (IEnumerable<string>)TagsSource;
            if (possibleTags == null)
                return;

            var tokens = new List<string>();
            var currentTag = new StringBuilder();
            var currentTagStartedAt = 0;

            var text = TextBox.Text;
            var caretIndex = TextBox.CaretIndex;
            string currentSearch = null;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];

                if (c == ';' || c == ',' || c == ' ' || c == '\t' || c == '\r' || c == '\n')
                {
                    if (currentTag.Length > 0)
                    {
                        if (caretIndex >= currentTagStartedAt && caretIndex <= currentTagStartedAt + currentTag.Length)
                        {
                            tokens.Add("{0}");
                            currentSearch = currentTag.ToString();
                        }
                        else
                        {
                            tokens.Add(currentTag.ToString());
                        }

                        currentTagStartedAt = i + 1;
                        currentTag.Clear();
                    }
                }
                else
                {
                    currentTag.Append(c);
                }
            }

            if (currentTag.Length > 0)
            {
                if (caretIndex >= currentTagStartedAt && caretIndex <= currentTagStartedAt + currentTag.Length)
                {
                    tokens.Add("{0}");
                    currentSearch = currentTag.ToString();
                }
                else
                {
                    tokens.Add(currentTag.ToString());
                }
            }

            var formatStringText = string.Join(" ", tokens);
            if (!formatStringText.Contains("{0}")) formatStringText += " {0}";

            formatStringText = formatStringText.Trim();

            var values = (
                from role in possibleTags
                where !tokens.Contains(role)
                where string.IsNullOrWhiteSpace(currentSearch) || role.IndexOf(currentSearch, StringComparison.OrdinalIgnoreCase) >= 0
                select new TagEntry
                {
                    Name = role,
                    Value = string.Format(formatStringText, role)
                }).ToList();

            ItemsSource = values;
            PopulateComplete();
        }

        private class TagEntry
        {
            public string Value { get; set; }
            public string Name { get; set; }
        }
    }
}