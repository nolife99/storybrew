using StorybrewCommon.Animations;
using StorybrewCommon.Mapset;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding.Commands;
using StorybrewCommon.Storyboarding.CommandValues;
using StorybrewCommon.Util;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;

namespace StorybrewCommon.Storyboarding.Util
{
    ///<summary> Generates commands on an <see cref="OsbSprite"/> based on the states of that sprite. </summary>
    public class CommandGenerator
    {
        readonly KeyframedValue<CommandPosition>
            positions = new KeyframedValue<CommandPosition>(InterpolatingFunctions.Vector2),
            finalPositions = new KeyframedValue<CommandPosition>(InterpolatingFunctions.Vector2);

        readonly KeyframedValue<CommandScale>
            scales = new KeyframedValue<CommandScale>(InterpolatingFunctions.Scale),
            finalScales = new KeyframedValue<CommandScale>(InterpolatingFunctions.Scale);

        readonly KeyframedValue<float>
            rotations = new KeyframedValue<float>(InterpolatingFunctions.FloatAngle),
            fades = new KeyframedValue<float>(InterpolatingFunctions.Float),
            finalRotations = new KeyframedValue<float>(InterpolatingFunctions.FloatAngle),
            finalfades = new KeyframedValue<float>(InterpolatingFunctions.Float);

        readonly KeyframedValue<CommandColor>
            colors = new KeyframedValue<CommandColor>(InterpolatingFunctions.CommandColor),
            finalColors = new KeyframedValue<CommandColor>(InterpolatingFunctions.CommandColor);

        readonly KeyframedValue<bool>
            flipH = new KeyframedValue<bool>(InterpolatingFunctions.BoolFrom),
            flipV = new KeyframedValue<bool>(InterpolatingFunctions.BoolFrom),
            additive = new KeyframedValue<bool>(InterpolatingFunctions.BoolFrom);

        readonly List<State> states = new List<State>();

        ///<summary> Gets the <see cref="CommandGenerator"/>'s start state. </summary>
        public State StartState => states.Count == 0 ? null : states[0];

        ///<summary> Gets the <see cref="CommandGenerator"/>'s end state. </summary>
        public State EndState => states.Count == 0 ? null : states[states.Count - 1];

        ///<summary> The tolerance threshold for position keyframe simplification. </summary>
        public double PositionTolerance = 1;

        ///<summary> The tolerance threshold for scaling keyframe simplification. </summary>
        public double ScaleTolerance = 1;

        ///<summary> The tolerance threshold for rotation keyframe simplification. </summary>
        public double RotationTolerance = .005;

        ///<summary> The tolerance threshold for coloring keyframe simplification. </summary>
        public double ColorTolerance = 1.5;

        ///<summary> The tolerance threshold for opacity keyframe simplification. </summary>
        public double OpacityTolerance = .1;

        ///<summary> The amount of decimal digits for position keyframes. </summary>
        public int PositionDecimals = 1;

        ///<summary> The amount of decimal digits for scaling keyframes. </summary>
        public int ScaleDecimals = 3;

        ///<summary> The amount of decimal digits for rotation keyframes. </summary>
        public int RotationDecimals = 5;

        ///<summary> The amount of decimal digits for opacity keyframes. </summary>
        public int OpacityDecimals = 1;

        internal KeyframedValue<CommandScale> scale = null;

        /// <summary>
        /// Adds a <see cref="State"/> to this instance. The <see cref="State"/> will be automatically sorted.
        /// </summary>
        public void Add(State state)
        {
            var count = states.Count;
            if (count == 0 || states[count - 1].Time < state.Time)
            {
                states.Add(state);
                return;
            }

            var i = states.BinarySearch(state, new State());
            if (i >= 0) while (i < count - 1 && states[i + 1].Time <= state.Time) i++;
            else i = ~i;

            states.Insert(i, state);
        }

        ///<summary> Generates commands on a sprite based on this generator's states. </summary>
        ///<param name="sprite"> The <see cref="OsbSprite"/> to have commands generated on. </param>
        ///<param name="action"> Encapsulates a group of commands to be generated on <paramref name="sprite"/>. </param>
        ///<param name="startTime"> The explicit start time of the command generation. Can be left <see langword="null"/> if <see cref="State.Time"/> is used. </param>
        ///<param name="endTime"> The explicit end time of the command generation. Can be left <see langword="null"/> if <see cref="State.Time"/> is used. </param>
        ///<param name="timeOffset"> The time offset of the command times. </param>
        ///<param name="loopable"> Whether the commands to be generated are contained within a <see cref="LoopCommand"/>. </param>
        ///<returns> <see langword="true"/> if any commands were generated, else returns <see langword="false"/>. </returns>
        public bool GenerateCommands(OsbSprite sprite, Action<Action, OsbSprite> action = null, double? startTime = null, double? endTime = null, double timeOffset = 0, bool loopable = false)
            => GenerateCommands(sprite, OsuHitObject.WidescreenStoryboardBounds, action, startTime, endTime, timeOffset, loopable);

        ///<summary> Generates commands on a sprite based on this generator's states. </summary>
        ///<param name="sprite"> The <see cref="OsbSprite"/> to have commands generated on. </param>
        ///<param name="bounds"> The rectangular boundary for the sprite to be generated within. </param>
        ///<param name="action"> Encapsulates a group of commands to be generated on <paramref name="sprite"/>. </param>
        ///<param name="startTime"> The explicit start time of the command generation. Can be left <see langword="null"/> if <see cref="State.Time"/> is used. </param>
        ///<param name="endTime"> The explicit end time of the command generation. Can be left <see langword="null"/> if <see cref="State.Time"/> is used. </param>
        ///<param name="timeOffset"> The time offset of the command times. </param>
        ///<param name="loopable"> Whether the commands to be generated are contained within a <see cref="LoopCommand"/>. </param>
        ///<returns> <see langword="true"/> if any commands were generated, else returns <see langword="false"/>. </returns>
        public bool GenerateCommands(OsbSprite sprite, RectangleF bounds, Action<Action, OsbSprite> action = null, double? startTime = null, double? endTime = null, double timeOffset = 0, bool loopable = false)
        {
            State previousState = null;
            bool wasVisible = false, everVisible = false, stateAdded = false;

            var imageSize = BitmapDimensions(sprite.TexturePath);
            states.ForEach(state =>
            {
                var time = state.Time + timeOffset;
                var isVisible = state.IsVisible(imageSize, sprite.Origin, bounds, this);

                if (isVisible && everVisible != true) everVisible = true;
                if (!wasVisible && isVisible)
                {
                    if (!stateAdded && previousState != null) addKeyframes(previousState, loopable ? time : previousState.Time + timeOffset);
                    addKeyframes(state, time);
                    if (stateAdded != true) stateAdded = true;
                }
                else if (wasVisible && !isVisible)
                {
                    addKeyframes(state, time);
                    commitKeyframes(imageSize);
                    if (stateAdded != true) stateAdded = true;
                }
                else if (isVisible)
                {
                    addKeyframes(state, time);
                    if (stateAdded != true) stateAdded = true;
                }
                else stateAdded = false;

                previousState = state;
                wasVisible = isVisible;
            });

            states.Clear();
            states.TrimExcess();

            if (wasVisible) commitKeyframes(imageSize);
            if (everVisible)
            {
                if (action is null) convertToCommands(sprite, startTime, endTime, timeOffset, imageSize, loopable);
                else action(() => convertToCommands(sprite, startTime, endTime, timeOffset, imageSize, loopable), sprite);
            }

            clearKeyframes();
            return everVisible;
        }
        void commitKeyframes(Size imageSize)
        {
            positions.Simplify2dKeyframes(PositionTolerance, s => s);
            positions.TransferKeyframes(finalPositions);

            scales.Simplify2dKeyframes(ScaleTolerance, v => new Vector2(v.X * imageSize.Width, v.Y * imageSize.Height));
            scales.TransferKeyframes(finalScales);

            rotations.Simplify1dKeyframes(RotationTolerance, r => r);
            rotations.TransferKeyframes(finalRotations);

            colors.Simplify3dKeyframes(ColorTolerance, c => new Vector3(c.R, c.G, c.B));
            colors.TransferKeyframes(finalColors);

            fades.Simplify1dKeyframes(OpacityTolerance, f => f);
            if (Math.Round(fades.StartValue, OpacityDecimals) > 0) fades.Add(fades.StartTime, 0, before: true);
            if (Math.Round(fades.EndValue, OpacityDecimals) > 0) fades.Add(fades.EndTime, 0);
            fades.TransferKeyframes(finalfades);
        }
        void convertToCommands(OsbSprite sprite, double? startTime, double? endTime, double timeOffset, Size imageSize, bool loopable)
        {
            var startState = loopable ? (startTime ?? StartState.Time) + timeOffset : (double?)null;
            var endState = loopable ? (endTime ?? EndState.Time) + timeOffset : (double?)null;

            var first = finalPositions.FirstOrDefault().Value;
            double checkPos(double value) => Math.Round(value, PositionDecimals);
            bool moveX = finalPositions.All(k => checkPos(k.Value.Y) == checkPos(first.Y)), moveY = finalPositions.All(k => checkPos(k.Value.X) == checkPos(first.X));
            finalPositions.ForEachPair((s, e) =>
            {
                if (moveX && !moveY)
                {
                    sprite.MoveX(s.Time, e.Time, s.Value.X, e.Value.X);
                    sprite.InitialPosition = new Vector2(0, s.Value.Y);
                }
                else if (moveY && !moveX)
                {
                    sprite.MoveY(s.Time, e.Time, s.Value.Y, e.Value.Y);
                    sprite.InitialPosition = new Vector2(s.Value.X, 0);
                }
                else sprite.Move(s.Time, e.Time, s.Value, e.Value);
            }, new Vector2(320, 240), p => new Vector2((float)Math.Round(p.X, PositionDecimals), (float)Math.Round(p.Y, PositionDecimals)), startState, loopable: loopable);

            int checkScale(double value) => (int)(value * Math.Max(imageSize.Width, imageSize.Height));
            double checkRound(double value) => Math.Round(value, ScaleDecimals);
            var vec = finalScales.Any(k => checkScale(k.Value.X) != checkScale(k.Value.Y));
            if (vec) vec = finalScales.Any(k => checkRound(k.Value.X) != checkRound(k.Value.Y));
            finalScales.ForEachPair((s, e) =>
            {
                if (vec) sprite.ScaleVec(s.Time, e.Time, s.Value, e.Value);
                else sprite.Scale(s.Time, e.Time, s.Value.X, e.Value.X);
            }, Vector2.One, s => new Vector2((float)Math.Round(s.X, ScaleDecimals), (float)Math.Round(s.Y, ScaleDecimals)), startState, loopable: loopable);

            finalRotations.ForEachPair((s, e) => sprite.Rotate(s.Time, e.Time, s.Value, e.Value),
                0, r => (float)Math.Round(r, RotationDecimals), startState, loopable: loopable);

            finalColors.ForEachPair((s, e) => sprite.Color(s.Time, e.Time, s.Value, e.Value), CommandColor.White,
                c => CommandColor.FromRgb(c.R, c.G, c.B), startState, loopable: loopable);

            finalfades.ForEachPair((s, e) =>
            {
                // what the hell???
                if (!(s.Time == sprite.CommandsStartTime && s.Time == e.Time && e.Value == 1 ||
                    s.Time == sprite.CommandsEndTime && s.Time == e.Time && e.Value == 0))
                    sprite.Fade(s.Time, e.Time, s.Value, e.Value);
            }, -1, o => (float)Math.Round(o, OpacityDecimals), startState, endState, loopable: loopable);

            flipH.ForEachFlag((f, t) => sprite.FlipH(f, t));
            flipV.ForEachFlag((f, t) => sprite.FlipV(f, t));
            additive.ForEachFlag((f, t) => sprite.Additive(f, t));
        }
        void addKeyframes(State state, double time)
        {
            positions.Add(time, state.Position);
            scales.Add(time, state.Scale);
            rotations.Add(time, (float)state.Rotation);
            colors.Add(time, state.Color);
            fades.Add(time, (float)state.Opacity);
            flipH.Add(time, state.FlipH);
            flipV.Add(time, state.FlipV);
            additive.Add(time, state.Additive);
        }
        void clearKeyframes()
        {
            positions.Clear(true);
            scales.Clear(true);
            rotations.Clear(true);
            colors.Clear(true);
            fades.Clear(true);
            finalPositions.Clear(true);
            finalScales.Clear(true);
            finalRotations.Clear(true);
            finalColors.Clear(true);
            finalfades.Clear(true);
            flipH.Clear(true);
            flipV.Clear(true);
            additive.Clear(true);
        }
        internal static Size BitmapDimensions(string path)
        {
            var src = StoryboardObjectGenerator.Current.getTrimmedBitmap(path, 
                StoryboardObjectGenerator.Current.GetMapsetBitmap(path, StoryboardObjectGenerator.Current.fontDirectories.Count == 0));
            return new Size(src.Width, src.Height);
        }
    }

    ///<summary> Defines all of an <see cref="OsbSprite"/>'s states as a class. </summary>
    public class State : IComparer<State>
    {
        ///<summary> Represents the base time, in milliseconds, of this state. </summary>
        public double Time;

        ///<summary> Represents the rotation, in radians, of this state. </summary>
        public double Rotation = 0;

        ///<summary> Represents the opacity, from 0 to 1, of this state. </summary>
        public double Opacity = 0;

        ///<summary> Represents the position, in osu!pixels, of this state. </summary>
        public CommandPosition Position = new Vector2(320, 240);

        ///<summary> Represents the scale, in osu!pixels, of this state. </summary>
        public CommandScale Scale = Vector2.One;

        ///<summary> Represents the color, in RGB values, of this state. </summary>
        public CommandColor Color = CommandColor.White;

        ///<summary> Represents the horizontal flip condition of this state. </summary>
        public bool FlipH;

        ///<summary> Represents the vertical flip condition of this state. </summary>
        public bool FlipV;

        ///<summary> Represents the additive toggle condition of this state. </summary>
        public bool Additive;

        /// <summary> 
        /// Returns the visibility of the sprite in the current <see cref="State"/> based on its image size, <see cref="OsbOrigin"/>, and screen boundaries. 
        /// </summary>
        /// <returns> <see langword="true"/> if the sprite is within <paramref name="bounds"/>, else returns <see langword="false"/>. </returns>
        public bool IsVisible(Size imageSize, OsbOrigin origin, RectangleF bounds, CommandGenerator generator = null)
        {
            if (Additive && Color == CommandColor.Black ||
                (generator is null ? Opacity : Math.Round(Opacity, generator.OpacityDecimals)) <= 0 ||
                Scale.X == 0 || Scale.Y == 0)
                return false;

            if (!bounds.Contains(
                generator is null ? (float)Position.X : (float)Math.Round(Position.X, generator.PositionDecimals),
                generator is null ? (float)Position.Y : (float)Math.Round(Position.Y, generator.PositionDecimals)))
            {
                var w = imageSize.Width * Scale.X;
                var h = imageSize.Height * Scale.Y;
                var originVector = OsbSprite.GetOriginVector(origin, w, h);

                var obb = new OrientedBoundingBox(new OpenTK.Vector2(Position.X, Position.Y), originVector, w, h, Rotation);
                if (!obb.Intersects(bounds)) return false;
            }
            return true;
        }

        int IComparer<State>.Compare(State x, State y) => x.Time.CompareTo(y.Time);
    }
}