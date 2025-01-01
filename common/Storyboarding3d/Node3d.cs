namespace StorybrewCommon.Storyboarding3d;

using System.Numerics;
using Animations;

///<summary> Represents a node in 3D world space. </summary>
public class Node3d : Object3d
{
    ///<summary> The node's X-position keyframes in the 3D world. </summary>
    public readonly KeyframedValue<float> PositionX = new(InterpolatingFunctions.Float);

    ///<summary> The node's Y-position keyframes in the 3D world. </summary>
    public readonly KeyframedValue<float> PositionY = new(InterpolatingFunctions.Float);

    ///<summary> The node's Z-position keyframes in the 3D world. </summary>
    public readonly KeyframedValue<float> PositionZ = new(InterpolatingFunctions.Float);

    ///<summary> The node's quaternion rotation keyframes. </summary>
    public readonly KeyframedValue<Quaternion> Rotation = new(InterpolatingFunctions.QuaternionSlerp, Quaternion.Identity);

    ///<summary> The node's relative X-scale keyframes of children objects. </summary>
    public readonly KeyframedValue<float> ScaleX = new(InterpolatingFunctions.Float, 1);

    ///<summary> The node's relative Y-scale keyframes of children objects. </summary>
    public readonly KeyframedValue<float> ScaleY = new(InterpolatingFunctions.Float, 1);

    ///<summary> The node's relative Z-scale keyframes of children objects. </summary>
    public readonly KeyframedValue<float> ScaleZ = new(InterpolatingFunctions.Float, 1);

    /// <inheritdoc/>
    public override Matrix4x4 WorldTransformAt(float time) => Matrix4x4.Multiply(
        Matrix4x4.Multiply(
            Matrix4x4.CreateScale(ScaleX.ValueAt(time), ScaleY.ValueAt(time), ScaleZ.ValueAt(time)),
            Matrix4x4.CreateFromQuaternion(Rotation.ValueAt(time))),
        Matrix4x4.CreateTranslation(PositionX.ValueAt(time), PositionY.ValueAt(time), PositionZ.ValueAt(time)));
}