using System;
using System.Windows.Controls;
using Octopus.Diagnostics;
using Octopus.Shared.Diagnostics;

namespace Octopus.Manager.Tentacle.Infrastructure
{
    public class TextBoxLogger : SystemLog
    {
        readonly TextBox textBox;

        public TextBoxLogger(TextBox textBox)
        {
            this.textBox = textBox;
        }

        public void Clear()
        {
            if (textBox.Dispatcher.CheckAccess())
            {
                textBox.Clear();
                textBox.ScrollToEnd();
            }
            else
            {
                textBox.Dispatcher.Invoke(Clear);
            }
        }

        protected override void WriteEvent(LogEvent logEvent)
        {
            var message = logEvent.MessageText;
            if (logEvent.Category > LogCategory.Info)
            {
                message = logEvent.Category + ": " + logEvent.MessageText;
            }

            WriteLine(message);
            if (logEvent.Error != null)
            {
                WriteLine(logEvent.Error.ToString());
            }
        }

        public override void Flush()
        {
        }

        public override bool IsEnabled(LogCategory category)
        {
            return true;
        }

        void WriteLine(string line)
        {
            if (textBox.Dispatcher.CheckAccess())
            {
                textBox.AppendText(line);
                textBox.AppendText(Environment.NewLine);
                textBox.ScrollToEnd();
            }
            else
            {
                textBox.Dispatcher.BeginInvoke(new Action(() => WriteLine(line)));
            }
        }
    }
}