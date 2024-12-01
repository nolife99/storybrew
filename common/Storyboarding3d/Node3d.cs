namespace StorybrewCommon.Storyboarding3d;

using System.Numerics;
using Animations;

///<summary> Represents a node in the three-dimensional world space. </summary>
public class Node3d : Object3d
{
    ///<summary> Represents the node's X-position in the 3D world. </summary>
    public readonly KeyframedValue<float> PositionX = new(InterpolatingFunctions.Float);

    ///<summary> Represents the node's Y-position in the 3D world. </summary>
    public readonly KeyframedValue<float> PositionY = new(InterpolatingFunctions.Float);

    ///<summary> Represents the node's Z-position in the 3D world. </summary>
    public readonly KeyframedValue<float> PositionZ = new(InterpolatingFunctions.Float);

    ///<summary> Represents the node's quaternion rotation about the origin. </summary>
    public readonly KeyframedValue<Quaternion> Rotation = new(InterpolatingFunctions.QuaternionSlerp, Quaternion.Identity);

    ///<summary> Represents the node's relative X-scale of children objects. </summary>
    public readonly KeyframedValue<float> ScaleX = new(InterpolatingFunctions.Float, 1);

    ///<summary> Represents the node's relative Y-scale of children objects. </summary>
    public readonly KeyframedValue<float> ScaleY = new(InterpolatingFunctions.Float, 1);

    ///<summary> Represents the node's relative Z-scale of children objects. </summary>
    public readonly KeyframedValue<float> ScaleZ = new(InterpolatingFunctions.Float, 1);

    /// <inheritdoc/>
    public override Matrix4x4 WorldTransformAt(float time) => Matrix4x4.Multiply(
        Matrix4x4.Multiply(Matrix4x4.CreateScale(ScaleX.ValueAt(time), ScaleY.ValueAt(time), ScaleZ.ValueAt(time)),
            Matrix4x4.CreateFromQuaternion(Rotation.ValueAt(time))),
        Matrix4x4.CreateTranslation(PositionX.ValueAt(time), PositionY.ValueAt(time), PositionZ.ValueAt(time)));
}