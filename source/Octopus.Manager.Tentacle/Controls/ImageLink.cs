using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Octopus.Manager.Tentacle.Controls
{
    public class ImageLink : Control
    {
        // Using a DependencyProperty as the backing store for Text.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(ImageLink), new PropertyMetadata(null));

        // Using a DependencyProperty as the backing store for BigImage.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty BigImageProperty =
            DependencyProperty.Register("BigImage", typeof(ImageSource), typeof(ImageLink), new PropertyMetadata(null));

        // Using a DependencyProperty as the backing store for SmallImage.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SmallImageProperty =
            DependencyProperty.Register("SmallImage", typeof(ImageSource), typeof(ImageLink), new PropertyMetadata(null));

        static ImageLink()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ImageLink), new FrameworkPropertyMetadata(typeof(ImageLink)));
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public ImageSource BigImage
        {
            get => (ImageSource)GetValue(BigImageProperty);
            set => SetValue(BigImageProperty, value);
        }

        public ImageSource SmallImage
        {
            get => (ImageSource)GetValue(SmallImageProperty);
            set => SetValue(SmallImageProperty, value);
        }

        public event EventHandler Click;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            var link = (Hyperlink)GetTemplateChild("PART_Link");
            if (link == null)
                return;

            link.Click += (sender, args) =>
            {
                var click = Click;
                click?.Invoke(this, EventArgs.Empty);
            };
        }
    }
}