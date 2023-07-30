using OpenTK;
using System;
using System.Collections.Generic;

namespace StorybrewCommon.Curves
{
    ///<summary> Represents a composite curve that is constructed from multiple curves. </summary>
    [Serializable] public class CompositeCurve : Curve
    {
        readonly List<Curve> curves;

        ///<summary> Returns a readonly list of curves that makes up the composite curve. </summary>
        public IReadOnlyList<Curve> Curves => curves;

        ///<inheritdoc/>
        public Vector2 StartPosition => curves[0].StartPosition;

        ///<inheritdoc/>
        public Vector2 EndPosition => curves[curves.Count - 1].EndPosition;

        ///<inheritdoc/>
        public double Length
        {
            get
            {
                var length = 0d;
                curves.ForEach(curve => length += curve.Length);
                return length;
            }
        }

        ///<summary> Constructs a composite curve from a list of curves <paramref name="curves"/>. </summary>
        public CompositeCurve(IEnumerable<Curve> curves) => this.curves = new List<Curve>(curves);

        ///<inheritdoc/>
        public Vector2 PositionAtDistance(double distance)
        {
            for (var i = 0; i < curves.Count; ++i)
            {
                var curve = curves[i];
                if (distance < curve.Length) return curve.PositionAtDistance(distance);
                distance -= curve.Length;
            }
            return curves[curves.Count - 1].EndPosition;
        }

        ///<inheritdoc/>
        public Vector2 PositionAtDelta(double delta)
        {
            var length = Length;

            var d = delta;
            for (var i = 0; i < curves.Count; ++i)
            {
                var curve = curves[i];
                var curveDelta = curve.Length / length;

                if (d < curveDelta) return curve.PositionAtDelta(d / curveDelta);
                d -= curveDelta;
            }
            return EndPosition;
        }
    }
}