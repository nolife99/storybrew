using System;
using System.Collections.Generic;
using StorybrewCommon.Storyboarding.CommandValues;

namespace StorybrewCommon.Curves;

///<summary> Represents a perfect circular curve. </summary>
///<remarks> Constructs a perfect circular curve from given control points. </remarks>
[Serializable] public class CircleCurve(CommandPosition startPoint, CommandPosition midPoint, CommandPosition endPoint) : BaseCurve
{
    ///<inheritdoc/>
    public override CommandPosition StartPosition => startPoint;

    ///<inheritdoc/>
    public override CommandPosition EndPosition => endPoint;

    ///<summary/>
    protected override void Initialize(List<(float, CommandPosition)> distancePosition, out double length)
    {
        var d = 2 * (startPoint.X * (midPoint.Y - endPoint.Y) + midPoint.X * (endPoint.Y - startPoint.Y) + endPoint.X * (startPoint.Y - midPoint.Y));
        if (d == 0) throw new ArgumentException("Invalid circle curve");

        var startPointLS = startPoint.LengthSquared;
        var midPointLS = midPoint.LengthSquared;
        var endPointLS = endPoint.LengthSquared;

        CommandPosition centre = new(
            (startPointLS * (midPoint.Y - endPoint.Y) + midPointLS * (endPoint.Y - startPoint.Y) + endPointLS * (startPoint.Y - midPoint.Y)) / d,
            (startPointLS * (endPoint.X - midPoint.X) + midPointLS * (startPoint.X - endPoint.X) + endPointLS * (midPoint.X - startPoint.X)) / d);
        var radius = (startPoint - centre).Length;

        var startAngle = Math.Atan2(startPoint.Y - centre.Y, startPoint.X - centre.X);
        var midAngle = Math.Atan2(midPoint.Y - centre.Y, midPoint.X - centre.X);
        var endAngle = Math.Atan2(endPoint.Y - centre.Y, endPoint.X - centre.X);

        while (midAngle < startAngle) midAngle += Math.Tau;
        while (endAngle < startAngle) endAngle += Math.Tau;
        if (midAngle > endAngle) endAngle -= Math.Tau;

        length = Math.Abs((endAngle - startAngle) * radius);
        var precision = (int)(length / 8);

        for (var i = 1; i < precision; i++)
        {
            var progress = (double)i / precision;
            var angle = endAngle * progress + startAngle * (1 - progress);

            distancePosition.Add(((float)(progress * length), new CommandPosition(Math.Cos(angle) * radius, Math.Sin(angle) * radius) + centre));
        }
        distancePosition.Add(((float)length, endPoint));
    }

    ///<summary> Returns whether or not the curve is a valid circle curve based on given control points. </summary>
    public static bool IsValid(CommandPosition startPoint, CommandPosition midPoint, CommandPosition endPoint)
        => startPoint != midPoint && midPoint != endPoint
        && 2 * (startPoint.X * (midPoint.Y - endPoint.Y) + midPoint.X * (endPoint.Y - startPoint.Y) + endPoint.X * (startPoint.Y - midPoint.Y)) != 0;
}