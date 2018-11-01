using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Octopus.Manager.Tentacle.Shell
{
    /// <summary>
    /// Interaction logic for TabbedWizard.xaml
    /// </summary>
    public partial class TabbedWizard : UserControl
    {
        public TabbedWizard()
        {
            InitializeComponent();
        }

        public void AddTab(TabItem tabItem)
        {
            tabItem.IsEnabled = false;
            tabs.Items.Add(tabItem);
        }

        public void AddTab(string header, ContentControl element)
        {
            var tabItem = new TabItem();
            tabItem.IsEnabled = false;

            // Needs to move to a template on the tab control.
            var badge = new Border
            {
                Visibility = Visibility.Collapsed,
                Background = Brushes.Orange,
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(3),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
                Child = new TextBlock
                {
                    Foreground = Brushes.White,
                    Text = "1",
                    MinWidth = 18,
                    TextAlignment = TextAlignment.Center
                }
            };

            var shellTab = element as ITabWithNotifications;
            if (shellTab != null)
            {
                if (shellTab.HasNotifications)
                    badge.Visibility = Visibility.Visible;
                shellTab.HasNotificationsChanged += (s, e) =>
                    badge.Visibility = shellTab.HasNotifications ? Visibility.Visible : Visibility.Collapsed;
            }

            tabItem.Header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new TextBlock {Text = header, VerticalAlignment = VerticalAlignment.Center},
                    badge
                }
            };
            tabItem.Content = element;
            tabs.Items.Add(tabItem);
        }

        async void SkipClicked(object sender, RoutedEventArgs e)
        {
            var current = tabs.SelectedItem as ITab;
            if (current != null)
            {
                var args = new CancelEventArgs();
                await current.OnSkip(args);
                if (args.Cancel)
                    return;
            }

            var visibleTabIndexes = GetVisibleTabIndexes();

            if (tabs.SelectedIndex == visibleTabIndexes.LastOrDefault())
            {
                Window.GetWindow(this)?.Close();
                return;
            }

            do
            {
                tabs.SelectedIndex++;
            } while (((TabView)tabs.SelectedItem).Visibility != Visibility.Visible && tabs.SelectedIndex < tabs.Items.Count - 1);

            RefreshWizardButtons();
        }

        async void NextClicked(object sender, EventArgs e)
        {
            var current = tabs.SelectedItem as ITab;
            if (current != null)
            {
                var args = new CancelEventArgs();
                await current.OnNext(args);
                if (args.Cancel)
                    return;
            }

            var visibleTabIndexes = GetVisibleTabIndexes();

            if (tabs.SelectedIndex == visibleTabIndexes.LastOrDefault())
            {
                Window.GetWindow(this).Close();
                return;
            }

            do
            {
                tabs.SelectedIndex++;
            } while (((TabView)tabs.SelectedItem).Visibility != Visibility.Visible && tabs.SelectedIndex < tabs.Items.Count - 1);

            RefreshWizardButtons();
        }

        void BackButtonClicked(object sender, EventArgs e)
        {
            var current = tabs.SelectedItem as ITab;
            if (current != null)
            {
                var args = new CancelEventArgs();
                current.OnBack(args);
                if (args.Cancel)
                    return;
            }

            if (tabs.SelectedIndex == 0)
                return;

            do
            {
                tabs.SelectedIndex--;
            } while (((TabView)tabs.SelectedItem).Visibility != Visibility.Visible && tabs.SelectedIndex > 0);
            RefreshWizardButtons();
        }

        void TabsSelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshWizardButtons();
        }

        void RefreshWizardButtons()
        {
            nextButton.Content = "NEXT";
            backButton.Content = "BACK";

            var visibleTabIndexes = GetVisibleTabIndexes();

            var selectedTab = tabs.SelectedItem as TabView;
            if (selectedTab != null)
            {
                ((TabView)tabs.SelectedItem).OnNavigateNext -= OnOnNavigateNext;
                ((TabView)tabs.SelectedItem).OnNavigateNext += OnOnNavigateNext;
                ((TabView)tabs.SelectedItem).IsViewed = true;
            }

            backButton.Visibility = Visibility.Visible;

            if (tabs.SelectedIndex == visibleTabIndexes.FirstOrDefault())
            {
                backButton.Visibility = Visibility.Collapsed;
            }

            if (tabs.SelectedIndex == visibleTabIndexes.LastOrDefault())
            {
                nextButton.Content = "FINISH";
            }

            for (var i = 0; i < tabs.Items.Count; i++)
            {
                ((TabView)tabs.Items[i]).IsPreviousTab = i < tabs.SelectedIndex;
            }
        }

        void OnOnNavigateNext()
        {
            NextClicked(null, EventArgs.Empty);
        }

        List<int> GetVisibleTabIndexes()
        {
            var visibleTabIndexes = tabs.Items.OfType<TabItem>().Select((v, i) => new {Index = i, Tab = v}).Where(v => v.Tab.Visibility == Visibility.Visible).Select(v => v.Index).ToList();
            return visibleTabIndexes;
        }
    }
}