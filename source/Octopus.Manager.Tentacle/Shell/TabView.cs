using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Octopus.Manager.Tentacle.Infrastructure;

namespace Octopus.Manager.Tentacle.Shell
{
    public class TabView : TabItem, ITab
    {
        public TabView()
        {
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                Loaded += (sender, args) => { Header = Content; };
            }
        }

        static TabView()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof (TabView), new FrameworkPropertyMetadata(typeof (TabView)));
        }

        public bool IsValid
        {
            get => (bool)GetValue(IsValidProperty);
            set => SetValue(IsValidProperty, value);
        }

        public string RuleSet
        {
            get => (string)GetValue(RuleSetProperty);
            set => SetValue(RuleSetProperty, value);
        }

        public ViewModel Model => ((ViewModel)DataContext);

        public bool IsSkipEnabled
        {
            get => (bool)GetValue(IsSkipEnabledProperty);
            set => SetValue(IsSkipEnabledProperty, value);
        }

        public bool IsNextEnabled
        {
            get => (bool)GetValue(IsNextEnabledProperty);
            set => SetValue(IsNextEnabledProperty, value);
        }

        public bool IsBackEnabled
        {
            get => (bool)GetValue(IsBackEnabledProperty);
            set => SetValue(IsBackEnabledProperty, value);
        }

        public bool IsViewed
        {
            get => (bool)GetValue(IsViewedProperty);
            set => SetValue(IsViewedProperty, value);
        }

        public bool IsPreviousTab
        {
            get => (bool)GetValue(IsPreviousTabProperty);
            set => SetValue(IsPreviousTabProperty, value);
        }

        public virtual void OnActivated()
        {
        }

        public virtual bool Validate()
        {
            Model.Validate();
            return Model.IsValid;
        }

        public event Action OnNavigateNext;

        protected virtual void NavigateNext()
        {
            OnNavigateNext?.Invoke();
        }

        public virtual void OnValidate()
        {
        }

        public virtual async Task OnSkip(CancelEventArgs e)
        {
            Model.PopRuleSet(RuleSet);
        }

        public virtual async Task OnNext(CancelEventArgs e)
        {
            Model.PushRuleSet(RuleSet);

            if (!Model.IsValid)
            {
                e.Cancel = true;
            }
        }

        public virtual void OnBack(CancelEventArgs e)
        {
            Model.PopRuleSet(RuleSet);
        }

        static void IsViewedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                var tab = (TabView) d;
                tab.OnActivated();
            }
        }

        public static readonly DependencyProperty IsSkipEnabledProperty = DependencyProperty.Register("IsSkipEnabled", typeof (bool), typeof (TabView), new PropertyMetadata(false));
        public static readonly DependencyProperty IsNextEnabledProperty = DependencyProperty.Register("IsNextEnabled", typeof (bool), typeof (TabView), new PropertyMetadata(true));
        public static readonly DependencyProperty IsBackEnabledProperty = DependencyProperty.Register("IsBackEnabled", typeof (bool), typeof (TabView), new PropertyMetadata(true));
        public static readonly DependencyProperty IsViewedProperty = DependencyProperty.Register("IsViewed", typeof (bool), typeof (TabView), new PropertyMetadata(false, IsViewedChanged));
        public static readonly DependencyProperty RuleSetProperty = DependencyProperty.Register("RuleSet", typeof (string), typeof (TabView), new PropertyMetadata(null));
        public static readonly DependencyProperty IsPreviousTabProperty = DependencyProperty.Register("IsPreviousTab", typeof (bool), typeof (TabView), new PropertyMetadata(false));
        public static readonly DependencyProperty IsValidProperty = DependencyProperty.Register("IsValid", typeof (bool), typeof (TabView), new PropertyMetadata(false));
    }
}
