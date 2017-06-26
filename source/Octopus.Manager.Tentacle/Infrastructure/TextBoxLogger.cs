using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Octopus.Diagnostics;
using Octopus.Shared.Diagnostics;

namespace Octopus.Manager.Tentacle.Infrastructure
{
    public class TextBoxLogger : AbstractLog
    {
        readonly TextBox textBox;

        public TextBoxLogger(TextBox textBox)
        {
            this.textBox = textBox;
        }

        public override LogContext CurrentContext => LogContext.Null();

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

        protected override void WriteEvents(IList<LogEvent> logEvents)
        {
            foreach (var log in logEvents) WriteEvent(log);
        }

        public override IDisposable WithinBlock(LogContext logContext)
        {
            return null;
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