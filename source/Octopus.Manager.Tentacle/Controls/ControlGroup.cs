using System.Windows;
using System.Windows.Controls;

namespace Octopus.Manager.Tentacle.Controls
{
    public class ControlGroup : HeaderedItemsControl
    {
        public static readonly DependencyProperty TargetProperty = DependencyProperty.Register("Target", typeof (object), typeof (ControlGroup), new PropertyMetadata(null));

        static ControlGroup()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof (ControlGroup), new FrameworkPropertyMetadata(typeof (ControlGroup)));
        }

        public object Target
        {
            get => GetValue(TargetProperty);
            set => SetValue(TargetProperty, value);
        }
    }
}