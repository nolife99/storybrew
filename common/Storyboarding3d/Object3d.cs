namespace StorybrewCommon.Storyboarding3d;

using System;
using System.Collections.Generic;
using System.Numerics;
using Animations;
using Storyboarding;
using Storyboarding.Commands;
using Storyboarding.CommandValues;
using Storyboarding.Util;

///<summary> Represents a basic 3D object. </summary>
public class Object3d
{
    readonly List<Object3d> children = [];

    ///<summary> A keyframed value representing this instance's color keyframes. </summary>
    public readonly KeyframedValue<CommandColor> Coloring = new(InterpolatingFunctions.CommandColor, CommandColor.White);

    ///<summary> A keyframed value representing this instance's opacity/fade keyframes. </summary>
    public readonly KeyframedValue<float> Opacity = new(InterpolatingFunctions.Float, 1);

    ///<summary> Whether or not this object's children should inherit its segment. </summary>
    public bool ChildrenInheritLayer = true;

    ///<summary> Whether or not this object's children should draw below it. </summary>
    public bool DrawBelowParent;

    ///<summary> Whether or not this object's children should inherit its coloring keyframes. </summary>
    public bool InheritsColor = true;

    ///<summary> Whether or not this object's children should inherit its opacity keyframes. </summary>
    public bool InheritsOpacity = true;

    ///<summary> Represents the instance's segment. </summary>
    public StoryboardSegment Segment;

    ///<summary> Adds a 3D sub-object to this instance. </summary>
    public void Add(Object3d child) => children.Add(child);

    ///<summary> Adds 3D sub-objects to this instance. </summary>
    public void AddRange(IEnumerable<Object3d> child) => children.AddRange(child);

    /// <summary> Gets this instance's 3D-world transform at <paramref name="time"/>. </summary>
    public virtual Matrix4x4 WorldTransformAt(float time) => Matrix4x4.Identity;

    /// <summary> Generates the base sprites for this instance. </summary>
    /// <param name="parentSegment"> The <see cref="StoryboardSegment"/> for the sprite to be generated in. </param>
    public void GenerateTreeSprite(StoryboardSegment parentSegment)
    {
        var layer = Segment ?? parentSegment;
        var childrenLayer = ChildrenInheritLayer ? layer : parentSegment;

        foreach (var child in children)
            if (child.DrawBelowParent)
                child.GenerateTreeSprite(childrenLayer);

        GenerateSprite(layer);
        foreach (var child in children)
            if (!child.DrawBelowParent)
                child.GenerateTreeSprite(childrenLayer);
    }

    /// <summary>
    ///     Generates a <see cref="State"/> for this instance at <paramref name="time"/> based on the given
    ///     <see cref="Camera"/>'s state.
    /// </summary>
    public void GenerateTreeStates(float time, Camera camera)
        => GenerateTreeStates(time, camera.StateAt(time), Object3dState.InitialState);

    /// <summary>
    ///     Generates a <see cref="State"/> for this instance at <paramref name="time"/> based on the given
    ///     <see cref="CameraState"/> and <see cref="Object3dState"/>.
    /// </summary>
    public void GenerateTreeStates(float time, CameraState camState, Object3dState parentState)
    {
        Object3dState state = new(Matrix4x4.Multiply(WorldTransformAt(time), parentState.WorldTransform),
            Coloring.ValueAt(time) * (InheritsColor ? parentState.Color : CommandColor.White),
            Opacity.ValueAt(time) * (InheritsOpacity ? parentState.Opacity : 1));

        GenerateStates(time, camState, state);
        foreach (var child in children) child.GenerateTreeStates(time, camState, state);
    }

    /// <summary> Generates commands on this instance's base sprites based on its <see cref="State"/>s. </summary>
    /// <param name="action"> Runs an action on this instance's sprites. </param>
    /// <param name="startTime">
    ///     The explicit start time of the commands (can be left <see langword="null"/> to use the
    ///     <see cref="State"/>'s time).
    /// </param>
    /// <param name="endTime">
    ///     The explicit end time of the commands (can be left <see langword="null"/> to use the
    ///     <see cref="State"/>'s time).
    /// </param>
    /// <param name="timeOffset"> The time offset of the commands. </param>
    /// <param name="loopable"> Whether the commands are encapsulated in a loop group. </param>
    public void GenerateTreeCommands(Action<Action, OsbSprite> action = null,
        float? startTime = null,
        float? endTime = null,
        float timeOffset = 0,
        bool loopable = false)
    {
        GenerateCommands(action, startTime, endTime, timeOffset, loopable);
        foreach (var child in children) child.GenerateTreeCommands(action, startTime, endTime, timeOffset, loopable);
    }

    /// <summary> Generates loop commands on this instance's base sprites based on its <see cref="State"/>s. </summary>
    /// <param name="action"> Runs an looping action on this instance's sprites. </param>
    /// <param name="startTime"> The explicit start time of the loop group. </param>
    /// <param name="endTime"> The explicit end time of the loop group. </param>
    /// <param name="loopCount">
    ///     The amount of times to loop within <paramref name="startTime"/> and
    ///     <paramref name="endTime"/>.
    /// </param>
    /// <param name="offsetCommands"> Whether to offset the commands to relative inside the loop. </param>
    public void GenerateTreeLoopCommands(float startTime,
        float endTime,
        int loopCount,
        Action<LoopCommand, OsbSprite> action = null,
        bool offsetCommands = true) => GenerateTreeCommands((commands, s) =>
    {
        var loop = s.StartLoopGroup(startTime, loopCount);
        commands();
        action?.Invoke(loop, s);
        s.EndGroup();
    }, startTime, endTime, offsetCommands ? -startTime : 0, true);

    /// <summary> Generates a <see cref="HasOsbSprites"/> object's sprites. </summary>
    /// <param name="segment"> The <see cref="StoryboardSegment"/> for the sprites to be generated in. </param>
    public virtual void GenerateSprite(StoryboardSegment segment) { }

    /// <summary>
    ///     Generates a <see cref="State"/> for this <see cref="HasOsbSprites"/> object at <paramref name="time"/>
    ///     based on the given <see cref="CameraState"/> and <see cref="Object3dState"/>.
    /// </summary>
    public virtual void GenerateStates(float time, CameraState cameraState, Object3dState object3dState) { }

    /// <summary>
    ///     Generates commands on this <see cref="HasOsbSprites"/> object's base sprites based on its
    ///     <see cref="State"/>s.
    /// </summary>
    /// <param name="action"> Runs an action on this object's sprites. </param>
    /// <param name="startTime">
    ///     The explicit start time of the commands (can be left <see langword="null"/> to use the
    ///     <see cref="State"/>'s time).
    /// </param>
    /// <param name="endTime">
    ///     The explicit end time of the commands (can be left <see langword="null"/> to use the
    ///     <see cref="State"/>'s time).
    /// </param>
    /// <param name="timeOffset"> The time offset of the commands. </param>
    /// <param name="loopable"> Whether or not the commands are encapsulated in a loop group. </param>
    public virtual void GenerateCommands(Action<Action, OsbSprite> action,
        float? startTime,
        float? endTime,
        float timeOffset,
        bool loopable) { }
}
#pragma warning disable CS1591
public readonly record struct Object3dState(Matrix4x4 WorldTransform, CommandColor Color, float Opacity)
{
    public static readonly Object3dState InitialState = new(Matrix4x4.Identity, CommandColor.White, 1);
}