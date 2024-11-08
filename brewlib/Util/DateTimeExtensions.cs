﻿namespace BrewLib.Util;

using System;
using System.Collections.Generic;
using System.Globalization;

public static class DateTimeExtensions
{
    static readonly Dictionary<long, string> thresholds = new()
    {
        [60] = "{0} seconds ago",
        [120] = "a minute ago",
        [2700] = "{0} minutes ago",
        [7200] = "an hour ago",
        [86400] = "{0} hours ago",
        [172800] = "yesterday",
        [2592000] = "{0} days ago",
        [5184000] = "a month ago",
        [31536000] = "{0} months ago",
        [63072000] = "a year ago",
        [long.MaxValue] = "{0} years ago"
    };

    public static string ToTimeAgo(this DateTimeOffset date)
    {
        var seconds = (DateTimeOffset.Now.Ticks - date.Ticks) * 1E-7;
        foreach (var threshold in thresholds)
            if (seconds < threshold.Key)
            {
                TimeSpan timespan = new(DateTimeOffset.Now.Ticks - date.Ticks);
                return string.Format(CultureInfo.InvariantCulture, threshold.Value, (timespan.Days > 365 ?
                    timespan.Days / 365 :
                    timespan.Days > 30 ? timespan.Days / 30 :
                        timespan.Days > 0 ? timespan.Days :
                            timespan.Hours > 0 ? timespan.Hours :
                                timespan.Minutes > 0 ? timespan.Minutes :
                                    timespan.Seconds > 0 ? timespan.Seconds :
                                        0).ToString(CultureInfo.InvariantCulture));
            }

        throw new InvalidOperationException();
    }
}