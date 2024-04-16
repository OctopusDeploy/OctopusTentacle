using System;
using System.Text;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace Octopus.Tentacle.CommonTestUtils.Logging
{
    public static class StringBuilderLogEventSinkExtensions
    {
        public static LoggerConfiguration StringBuilder(this LoggerSinkConfiguration configuration, StringBuilder stringBuilder, IFormatProvider? formatProvider = null)
            => configuration.Sink(new StringBuilderLogEventSink(stringBuilder, formatProvider));
    }
    public class StringBuilderLogEventSink : ILogEventSink
    {
        readonly StringBuilder stringBuilder;
        readonly IFormatProvider? formatProvider;

        public StringBuilderLogEventSink(StringBuilder stringBuilder, IFormatProvider? formatProvider)
        {
            this.stringBuilder = stringBuilder;
            this.formatProvider = formatProvider;
        }

        public void Emit(LogEvent logEvent)
        {
            var message = logEvent.RenderMessage(formatProvider);
            stringBuilder.AppendLine(message);
        }
    }
}