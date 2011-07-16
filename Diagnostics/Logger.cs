using System;
using System.Xml;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;

namespace Octopus.Shared.Diagnostics
{
    public static class Logger
    {
        const string LoggingConfiguration =
            @"<log4net>
  <root>
    <level value='DEBUG' />
    <appender-ref ref='ConsoleAppender' />
    <appender-ref ref='EventLogAppender' />
  </root>

  <logger name='Octopus'>
    <level value='DEBUG' />
  </logger>

  <appender name='ConsoleAppender' type='log4net.Appender.ColoredConsoleAppender'>
    <mapping>
      <level value='ERROR' />
      <foreColor value='Red, HighIntensity' />
    </mapping>
    <mapping>
      <level value='WARN' />
      <foreColor value='Yellow, HighIntensity' />
    </mapping>
    <mapping>
      <level value='Info' />
      <foreColor value='White, HighIntensity' />
    </mapping>
    <mapping>
      <level value='Debug' />
      <foreColor value='White' />
    </mapping>
    <layout type='log4net.Layout.PatternLayout'>
      <conversionPattern value='%message%newline' />
    </layout>
  </appender>

  <appender name='EventLogAppender' type='log4net.Appender.EventLogAppender'>
    <filter type='log4net.Filter.LevelRangeFilter'>
      <levelMin value='WARN' />
      <levelMax value='FATAL' />
    </filter>
    <param name='LogName' value='Application' />
    <param name='ApplicationName' value='Octopus' />
    <layout type='log4net.Layout.PatternLayout'>
      <conversionPattern value='%date [%thread] %-5level %logger [%property{NDC}] - %message%newline' />
    </layout>
  </appender>
</log4net>";

        public static ILog Default
        {
            get { return Nested.Log; }
        }

        public static void SetLevel(this ILoggerWrapper log, string levelName)
        {
            var logger = (log4net.Repository.Hierarchy.Logger) log.Logger;
            logger.Level = logger.Hierarchy.LevelMap[levelName];
        }

        public static void AddAppender(this ILoggerWrapper log, IAppender appender)
        {
            var logger = (log4net.Repository.Hierarchy.Logger) log.Logger;
            logger.AddAppender(appender);
        }

        #region Nested type: Nested

        static class Nested
        {
            public static readonly ILog Log;

            static Nested()
            {
                var document = new XmlDocument();
                document.LoadXml(LoggingConfiguration);

                Log = LogManager.GetLogger("Octopus");
                XmlConfigurator.Configure(Log.Logger.Repository, (XmlElement) document.GetElementsByTagName("log4net")[0]);
            }
        }

        #endregion
    }
}