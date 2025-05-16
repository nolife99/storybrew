namespace BrewLib.Util;

using System;
using System.Globalization;

public static class DateTimeExtensions
{
    static readonly (TimeSpan, string)[] thresholds =
    [
        (TimeSpan.FromMinutes(1), "{0} seconds ago"),
        (TimeSpan.FromMinutes(2), "a minute ago"),
        (TimeSpan.FromHours(1), "{0} minutes ago"),
        (TimeSpan.FromHours(2), "an hour ago"),
        (TimeSpan.FromDays(1), "{0} hours ago"),
        (TimeSpan.FromDays(2), "yesterday"),
        (TimeSpan.FromDays(30), "{0} days ago"),
        (TimeSpan.FromDays(60), "a month ago"),
        (TimeSpan.FromDays(365), "{0} months ago"),
        (TimeSpan.FromDays(730), "a year ago"),
        (TimeSpan.MaxValue, "{0} years ago")
    ];

    public static string ToTimeAgo(this DateTimeOffset date)
    {
        var timeSpan = DateTimeOffset.Now - date;
        foreach (var threshold in thresholds)
            if (timeSpan < threshold.Item1)
                return string.Format(CultureInfo.InvariantCulture, threshold.Item2,
                    (timeSpan.Days > 365 ? timeSpan.Days / 365 :
                        timeSpan.Days > 30 ? timeSpan.Days / 30 :
                        timeSpan.Days > 0 ? timeSpan.Days :
                        timeSpan.Hours > 0 ? timeSpan.Hours :
                        timeSpan.Minutes > 0 ? timeSpan.Minutes :
                        timeSpan.Seconds > 0 ? timeSpan.Seconds : 0).ToString(CultureInfo.InvariantCulture));

        throw new InvalidOperationException();
    }
}