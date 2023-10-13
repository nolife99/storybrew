using StorybrewCommon.Animations;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding.Commands;
using StorybrewCommon.Storyboarding.CommandValues;
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
            positions = new KeyframedValue<CommandPosition>(InterpolatingFunctions.Position),
            finalPositions = new KeyframedValue<CommandPosition>(InterpolatingFunctions.Position);

        readonly KeyframedValue<CommandScale>
            scales = new KeyframedValue<CommandScale>(InterpolatingFunctions.Scale),
            finalScales = new KeyframedValue<CommandScale>(InterpolatingFunctions.Scale);

        readonly KeyframedValue<double>
            rotations = new KeyframedValue<double>(InterpolatingFunctions.DoubleAngle),
            fades = new KeyframedValue<double>(InterpolatingFunctions.Double),
            finalRotations = new KeyframedValue<double>(InterpolatingFunctions.DoubleAngle),
            finalfades = new KeyframedValue<double>(InterpolatingFunctions.Double);

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

        /// <summary>
        /// Adds a <see cref="State"/> to this instance that will be automatically sorted.
        /// </summary>
        public void Add(State state)
        {
            var count = states.Count;
            if (count == 0 || states[count - 1].Time < state.Time)
            {
                states.Add(state);
                return;
            }

            var i = states.BinarySearch(state, state);
            if (i >= 0) while (i < count - 1 && states[i + 1].Time <= state.Time) ++i;
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
        {
            State previousState = null;
            bool wasVisible = false, everVisible = false, stateAdded = false;

            var imageSize = BitmapDimensions(sprite);
            states.ForEach(state =>
            {
                var time = state.Time + timeOffset;
                var isVisible = state.IsVisible(imageSize, sprite.Origin, this);

                if (isVisible && everVisible != true) everVisible = true;
                if (!wasVisible && isVisible)
                {
                    if (!stateAdded && previousState != null) addKeyframes(previousState, loopable ? time : (previousState.Time + timeOffset));
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

            if (wasVisible) commitKeyframes(imageSize);
            if (everVisible)
            {
                if (action is null) convertToCommands(sprite, startTime, endTime, timeOffset, imageSize, loopable);
                else action(() => convertToCommands(sprite, startTime, endTime, timeOffset, imageSize, loopable), sprite);
            }

            clearKeyframes();
            return everVisible;
        }
        void commitKeyframes(SizeF imageSize)
        {
            positions.Simplify2dKeyframes(PositionTolerance, s => s);
            if (finalPositions.EndTime != int.MinValue) finalPositions.DebugUntil(positions.StartTime);
            positions.TransferKeyframes(finalPositions);

            scales.Simplify2dKeyframes(ScaleTolerance, v => new Vector2(v.X * imageSize.Width, v.Y * imageSize.Height));
            if (finalScales.EndTime != int.MinValue) finalScales.DebugUntil(scales.StartTime);
            scales.TransferKeyframes(finalScales);

            rotations.Simplify1dKeyframes(RotationTolerance, r => (float)r);
            if (finalRotations.EndTime != int.MinValue) finalRotations.DebugUntil(rotations.StartTime);
            rotations.TransferKeyframes(finalRotations);

            colors.Simplify3dKeyframes(ColorTolerance, c => new Vector3(c.R, c.G, c.B));
            if (finalColors.EndTime != int.MinValue) finalColors.DebugUntil(colors.StartTime);
            colors.TransferKeyframes(finalColors);

            fades.Simplify1dKeyframes(OpacityTolerance, f => (float)f);
            if (Math.Round(fades.StartValue, OpacityDecimals) > 0) fades.Add(fades.StartTime, 0, true);
            if (Math.Round(fades.EndValue, OpacityDecimals) > 0) fades.Add(fades.EndTime, 0);
            fades.TransferKeyframes(finalfades);
        }
        void convertToCommands(OsbSprite sprite, double? startTime, double? endTime, double timeOffset, SizeF imageSize, bool loopable)
        {
            var startState = loopable ? (startTime ?? StartState.Time) + timeOffset : (double?)null;
            var endState = loopable ? (endTime ?? EndState.Time) + timeOffset : (double?)null;

            double checkPos(double value) => Math.Round(value, PositionDecimals);
            bool moveX = finalPositions.All(k => checkPos(k.Value.Y) == checkPos(finalPositions.StartValue.Y)), 
                 moveY = finalPositions.All(k => checkPos(k.Value.X) == checkPos(finalPositions.StartValue.X));

            finalPositions.ForEachPair((s, e) =>
            {
                if (moveX && !moveY)
                {
                    sprite.MoveX(s.Time, e.Time, s.Value.X, e.Value.X);
                    sprite.InitialPosition = new CommandPosition(0, s.Value.Y);
                }
                else if (moveY && !moveX)
                {
                    sprite.MoveY(s.Time, e.Time, s.Value.Y, e.Value.Y);
                    sprite.InitialPosition = new CommandPosition(s.Value.X, 0);
                }
                else sprite.Move(s.Time, e.Time, s.Value, e.Value);
            }, new Vector2(320, 240), p => new CommandPosition(Math.Round(p.X, PositionDecimals), Math.Round(p.Y, PositionDecimals)), startState, endState, loopable);

            int checkScale(double value) => (int)(value * Math.Max(imageSize.Width, imageSize.Height));
            var vec = finalScales.Any(k => Math.Abs(checkScale(k.Value.X) - checkScale(k.Value.Y)) >= 1);
            finalScales.ForEachPair((s, e) =>
            {
                if (vec) sprite.ScaleVec(s.Time, e.Time, s.Value, e.Value);
                else sprite.Scale(s.Time, e.Time, s.Value.X, e.Value.X);
            }, Vector2.One, s => new CommandScale(Math.Round(s.X, ScaleDecimals), Math.Round(s.Y, ScaleDecimals)), startState, endState, loopable);

            finalRotations.ForEachPair((s, e) => sprite.Rotate(s.Time, e.Time, s.Value, e.Value),
                0, r => Math.Round(r, RotationDecimals), startState, endState, loopable);

            finalColors.ForEachPair((s, e) => sprite.Color(s.Time, e.Time, s.Value, e.Value), CommandColor.White, null, startState, endState, loopable);
            finalfades.ForEachPair((s, e) =>
            {
                // what the hell???
                if (!(s.Time == sprite.StartTime && s.Time == e.Time && e.Value >= 1 ||
                    s.Time == sprite.EndTime || s.Time == EndState.Time && s.Time == e.Time && e.Value <= 0))
                    sprite.Fade(s.Time, e.Time, s.Value, e.Value);
            }, -1, o => Math.Round(o, OpacityDecimals), startState, endState, loopable);

            flipH.ForEachFlag((f, t) => sprite.FlipH(f, t));
            flipV.ForEachFlag((f, t) => sprite.FlipV(f, t));
            additive.ForEachFlag((f, t) => sprite.Additive(f, t));
        }
        void addKeyframes(State state, double time)
        {
            positions.Add(time, state.Position);
            scales.Add(time, state.Scale);
            rotations.Add(time, state.Rotation);
            colors.Add(time, state.Color);
            fades.Add(time, state.Opacity);
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
            states.Clear();
            states.TrimExcess();
        }
        internal static SizeF BitmapDimensions(OsbSprite sprite) => 
            StoryboardObjectGenerator.Current.GetMapsetBitmap(sprite.TexturePath, StoryboardObjectGenerator.Current.fontDirectories.Count == 0).PhysicalDimension;
    }

    ///<summary> Defines all of an <see cref="OsbSprite"/>'s states as a class. </summary>
    public class State : IComparer<State>
    {
        ///<summary> Represents the base time, in milliseconds, of this state. </summary>
        public double Time;

        ///<summary> Represents the rotation, in radians, of this state. </summary>
        public double Rotation;

        ///<summary> Represents the opacity, from 0 to 1, of this state. </summary>
        public double Opacity;

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
        /// Returns the visibility of the sprite in the current <see cref="State"/> based on its image dimensions and <see cref="OsbOrigin"/>. 
        /// </summary>
        /// <returns> <see langword="true"/> if the sprite is visible within widescreen boundaries, else returns <see langword="false"/>. </returns>
        public bool IsVisible(SizeF imageSize, OsbOrigin origin, CommandGenerator generator = null)
        {
            if (Additive && Color == CommandColor.Black ||
                (generator is null ? Opacity : Math.Round(Opacity, generator.OpacityDecimals)) == 0 ||
                (generator is null ? (double)Scale.X : Math.Round(Scale.X, generator.ScaleDecimals)) <= 0 ||
                (generator is null ? (double)Scale.Y : Math.Round(Scale.Y, generator.ScaleDecimals)) <= 0)
                return false;

            var rounded = new CommandPosition(
                generator is null ? (double)Position.X : Math.Round(Position.X, generator.PositionDecimals),
                generator is null ? (double)Position.Y : Math.Round(Position.Y, generator.PositionDecimals));

            return OsbSprite.InScreenBounds(rounded, new SizeF(imageSize.Width * Scale.X, imageSize.Height * Scale.Y), 
                generator is null ? Rotation : Math.Round(Rotation, generator.RotationDecimals), origin);
        }

        int IComparer<State>.Compare(State x, State y) => x.Time.CompareTo(y.Time);
    }
}