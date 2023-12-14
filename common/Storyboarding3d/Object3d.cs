using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BrewLib.Util;
using StorybrewCommon.Animations;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Storyboarding.Commands;
using StorybrewCommon.Storyboarding.CommandValues;
using StorybrewCommon.Storyboarding.Util;

namespace StorybrewCommon.Storyboarding3d;

///<summary> Represents a basic 3D object. </summary>
public class Object3d
{
    readonly List<Object3d> children = [];

    ///<summary> A keyframed value representing this instance's color keyframes. </summary>
    public readonly KeyframedValue<CommandColor> Coloring = new(InterpolatingFunctions.CommandColor, CommandColor.White);

    ///<summary> A keyframed value representing this instance's opacity/fade keyframes. </summary>
    public readonly KeyframedValue<float> Opacity = new(InterpolatingFunctions.Float, 1);

    ///<summary> Represents the instance's segment. </summary>
    public StoryboardSegment Segment;

    ///<summary> Whether or not this object's children should inherit its coloring keyframes. </summary>
    public bool InheritsColor = true;

    ///<summary> Whether or not this object's children should inherit its opacity keyframes. </summary>
    public bool InheritsOpacity = true;

    ///<summary> Whether or not this object's children should draw below it. </summary>
    public bool DrawBelowParent;

    ///<summary> Whether or not this object's children should inherit its segment. </summary>
    public bool ChildrenInheritLayer = true;

    ///<summary> Adds a 3D sub-object to this instance. </summary>
    public void Add(Object3d child) => children.Add(child);

    ///<summary> Adds 3D sub-objects to this instance. </summary>
    public void AddRange(IEnumerable<Object3d> child) => children.AddRange(child);

    ///<summary> Gets this instance's 3D-world transform at <paramref name="time"/>. </summary>
    public virtual Matrix4x4 WorldTransformAt(double time) => Matrix4x4.Identity;

    ///<summary> Generates the base sprites for this instance. </summary>
    ///<param name="parentSegment"> The <see cref="StoryboardSegment"/> for the sprite to be generated in. </param>
    public void GenerateTreeSprite(StoryboardSegment parentSegment)
    {
        var layer = Segment ?? parentSegment;
        var childrenLayer = ChildrenInheritLayer ? layer : parentSegment;

        children.ForEach(child => child.GenerateTreeSprite(childrenLayer), child => child.DrawBelowParent);
        GenerateSprite(layer);
        children.ForEach(child => child.GenerateTreeSprite(childrenLayer), child => !child.DrawBelowParent);
    }

    ///<summary> Generates a <see cref="State"/> for this instance at <paramref name="time"/> based on the given <see cref="Camera"/>'s state. </summary>
    public void GenerateTreeStates(double time, Camera camera) => GenerateTreeStates(time, camera.StateAt(time), Object3dState.InitialState);

    ///<summary> Generates a <see cref="State"/> for this instance at <paramref name="time"/> based on the given <see cref="CameraState"/> and <see cref="Object3dState"/>. </summary>
    public void GenerateTreeStates(double time, CameraState cameraState, Object3dState parent3dState)
    {
        Object3dState object3dState = new(
            WorldTransformAt(time) * parent3dState.WorldTransform,
            Coloring.ValueAt(time) * (InheritsColor ? parent3dState.Color : CommandColor.White),
            Opacity.ValueAt(time) * (InheritsOpacity ? parent3dState.Opacity : 1));

        GenerateStates(time, cameraState, object3dState);

        var span = CollectionsMarshal.AsSpan(children);
        ref var r0 = ref MemoryMarshal.GetReference(span);
        ref var rEnd = ref Unsafe.Add(ref r0, span.Length);

        while (Unsafe.IsAddressLessThan(ref r0, ref rEnd))
        {
            r0.GenerateTreeStates(time, cameraState, object3dState);
            r0 = ref Unsafe.Add(ref r0, 1);
        }
    }

    ///<summary> Generates commands on this instance's base sprites based on its <see cref="State"/>s. </summary>
    ///<param name="action"> Runs an action on this instance's sprites. </param>
    ///<param name="startTime"> The explicit start time of the commands (can be left <see langword="null"/> to use the <see cref="State"/>'s time). </param>
    ///<param name="endTime"> The explicit end time of the commands (can be left <see langword="null"/> to use the <see cref="State"/>'s time). </param>
    ///<param name="timeOffset"> The time offset of the commands. </param>
    ///<param name="loopable"> Whether or not the commands are encapsulated in a loop group. </param>
    public void GenerateTreeCommands(Action<Action, OsbSprite> action = null, double? startTime = null, double? endTime = null, double timeOffset = 0, bool loopable = false)
    {
        GenerateCommands(action, startTime, endTime, timeOffset, loopable);

        var span = CollectionsMarshal.AsSpan(children);
        ref var r0 = ref MemoryMarshal.GetReference(span);
        ref var rEnd = ref Unsafe.Add(ref r0, span.Length);

        while (Unsafe.IsAddressLessThan(ref r0, ref rEnd))
        {
            r0.GenerateTreeCommands(action, startTime, endTime, timeOffset, loopable);
            r0 = ref Unsafe.Add(ref r0, 1);
        }
    }

    ///<summary> Generates loop commands on this instance's base sprites based on its <see cref="State"/>s. </summary>
    ///<param name="action"> Runs an looping action on this instance's sprites. </param>
    ///<param name="startTime"> The explicit start time of the loop group. </param>
    ///<param name="endTime"> The explicit end time of the loop group. </param>
    ///<param name="loopCount"> The amount of times to loop within <paramref name="startTime"/> and <paramref name="endTime"/>. </param>
    ///<param name="offsetCommands"> Whether or not to offset the commands to relative inside the loop. </param>
    public void GenerateTreeLoopCommands(double startTime, double endTime, int loopCount, Action<LoopCommand, OsbSprite> action = null, bool offsetCommands = true)
        => GenerateTreeCommands((commands, s) =>
    {
        var loop = s.StartLoopGroup(startTime, loopCount);
        commands();
        action?.Invoke(loop, s);
        s.EndGroup();
    }, startTime, endTime, offsetCommands ? -startTime : 0, true);

    ///<summary> Generates a <see cref="HasOsbSprites"/> object's sprites. </summary>
    ///<param name="segment"> The <see cref="StoryboardSegment"/> for the sprites to be generated in. </param>
    public virtual void GenerateSprite(StoryboardSegment segment) { }

    ///<summary> Generates a <see cref="State"/> for this <see cref="HasOsbSprites"/> object at <paramref name="time"/> based on the given <see cref="CameraState"/> and <see cref="Object3dState"/>. </summary>
    public virtual void GenerateStates(double time, CameraState cameraState, Object3dState object3dState) { }

    ///<summary> Generates commands on this <see cref="HasOsbSprites"/> object's base sprites based on its <see cref="State"/>s. </summary>
    ///<param name="action"> Runs an action on this object's sprites. </param>
    ///<param name="startTime"> The explicit start time of the commands (can be left <see langword="null"/> to use the <see cref="State"/>'s time). </param>
    ///<param name="endTime"> The explicit end time of the commands (can be left <see langword="null"/> to use the <see cref="State"/>'s time). </param>
    ///<param name="timeOffset"> The time offset of the commands. </param>
    ///<param name="loopable"> Whether or not the commands are encapsulated in a loop group. </param>
    public virtual void GenerateCommands(Action<Action, OsbSprite> action, double? startTime, double? endTime, double timeOffset, bool loopable) { }
}

#pragma warning disable CS1591
public class Object3dState(Matrix4x4 worldTransform, CommandColor color, float opacity)
{
    public static readonly Object3dState InitialState = new(Matrix4x4.Identity, CommandColor.White, 1);
    public Matrix4x4 WorldTransform => worldTransform;
    public CommandColor Color => color;
    public float Opacity => opacity;
}