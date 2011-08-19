using System;

public static class DateTimeExtensions
{
    /// <summary>
    /// Returns the date and time formatted as, for example, 'Thursday, 18 August 2011 3:46 PM'.
    /// </summary>
    /// <param name="dateAndTime">The date and time.</param>
    /// <returns>The formatted date and time.</returns>
    public static string NormalFormatDateAndTime(this DateTime? dateAndTime)
    {
        return dateAndTime == null ? "" : NormalFormatDateAndTime(dateAndTime.Value);
    }

    /// <summary>
    /// Returns the date and time formatted as, for example, 'Thursday, 18 August 2011 3:46 PM'.
    /// </summary>
    /// <param name="dateAndTime">The date and time.</param>
    /// <returns>The formatted date and time.</returns>
    public static string NormalFormatDateAndTime(this DateTime dateAndTime)
    {
        dateAndTime = dateAndTime.ToLocalTime();
        return dateAndTime.ToString("f");
    }

    /// <summary>
    /// Returns the date formatted as, for example, '18 August' or '18 August 2009' for prior years.
    /// </summary>
    /// <param name="date">The date to format.</param>
    /// <returns></returns>
    public static string ShortFormatDate(this DateTime date)
    {
        date = date.ToLocalTime();
        if (date.Year == DateTime.Today.Year)
            return date.ToString("m");

        return date.ToString("m") + date.ToString(" yyyy");
    }
}
