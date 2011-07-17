using System;

public static class DateTimeExtensions
{
    public static string NormalFormat(this DateTime? time)
    {
        return time == null ? "" : NormalFormat(time.Value);
    }

    public static string NormalFormat(this DateTime time)
    {
        time = time.ToLocalTime();
        return time.ToString("f");
    }

    public static string ShortFormat(this DateTime time)
    {
        time = time.ToLocalTime();
        if (time.Year == DateTime.Today.Year)
            return time.ToString("m");

        return time.ToString("m") + time.ToString(" yyyy");
    }
}
