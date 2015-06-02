using System;
using System.Globalization;

namespace Octopus.Shared.Util
{
    public static class DateTimeExtensions
    {
        static readonly GregorianCalendar _gc = new GregorianCalendar();

        public static int GetWeekOfMonth(this DateTime time)
        {
            var first = new DateTime(time.Year, time.Month, 1);
            return time.GetWeekOfYear() - first.GetWeekOfYear() + 1;
        }

        static int GetWeekOfYear(this DateTime time)
        {
            return _gc.GetWeekOfYear(time, CalendarWeekRule.FirstDay, DayOfWeek.Sunday);
        }

        /// <summary>
        /// Returns the date and time formatted as, for example, 'Thursday, 18 August 2011 3:46 PM'.
        /// </summary>
        /// <param name="dateAndTime">The date and time.</param>
        /// <returns>The formatted date and time.</returns>
        public static string NormalFormatDateAndTime(this DateTimeOffset? dateAndTime)
        {
            return dateAndTime == null ? "" : NormalFormatDateAndTime(dateAndTime.Value);
        }

        /// <summary>
        /// Returns the date and time formatted as, for example, 'Thursday, 18 August 2011 3:46 PM'.
        /// </summary>
        /// <param name="dateAndTime">The date and time.</param>
        /// <returns>The formatted date and time.</returns>
        public static string NormalFormatDateAndTime(this DateTimeOffset dateAndTime)
        {
            dateAndTime = dateAndTime.ToLocalTime();
            return dateAndTime.ToString("f");
        }

        /// <summary>
        /// Returns the date formatted as, for example, '18 August' or '18 August 2009' for prior years.
        /// </summary>
        /// <param name="date">The date to format.</param>
        /// <returns></returns>
        public static string ShortFormatDate(this DateTimeOffset date)
        {
            date = date.ToLocalTime();
            if (date.Year == DateTime.Today.Year)
                return date.ToString("d MMMM");

            return date.ToString("d MMMM") + date.ToString(" yyyy");
        }

        /// <summary>
        /// Returns the date formatted as, for example, '18 August' or '18 August 2009' for prior years.
        /// </summary>
        /// <param name="date">The date to format.</param>
        /// <returns></returns>
        public static string ShortFormatTime(this DateTimeOffset date)
        {
            date = date.ToLocalTime();
            return date.LocalDateTime.ToShortTimeString();
        }
    }
}