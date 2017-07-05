using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Octopus.Manager.Tentacle.Controls
{
    public class RetinaImage : Control
    {
        static RetinaImage()
        {
            var dpiXProperty = typeof (SystemParameters).GetProperty("DpiX", BindingFlags.NonPublic | BindingFlags.Static);

            var dpiX = (int)dpiXProperty.GetValue(null, null);

            if (dpiX/96.00 > 1.05)
            {
                IsRetina = true;
            }
            else
            {
                IsRetina = false;
            }
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

        public ImageSource Image
        {
            get => (ImageSource)GetValue(ImageProperty);
            private set => SetValue(ImagePropertyKey, value);
        }

        static void OnImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = ((RetinaImage)d);
            if (IsRetina)
            {
                self.Image = self.BigImage ?? self.SmallImage;
            }
            else
            {
                self.Image = self.SmallImage ?? self.BigImage;
            }
        }

        static readonly DependencyPropertyKey ImagePropertyKey = DependencyProperty.RegisterReadOnly("Image", typeof (ImageSource), typeof (RetinaImage), new PropertyMetadata(null));
        public static readonly DependencyProperty ImageProperty = ImagePropertyKey.DependencyProperty;
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof (ImageSource), typeof (RetinaImage), new PropertyMetadata(null));
        public static readonly DependencyProperty BigImageProperty = DependencyProperty.Register("BigImage", typeof (ImageSource), typeof (RetinaImage), new PropertyMetadata(null, OnImageChanged));
        public static readonly DependencyProperty SmallImageProperty = DependencyProperty.Register("SmallImage", typeof (ImageSource), typeof (RetinaImage), new PropertyMetadata(null, OnImageChanged));
        static readonly bool IsRetina;
    }
}