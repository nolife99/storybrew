using System.Numerics;
using StorybrewCommon.Animations;

namespace StorybrewCommon.Storyboarding3d
{
    ///<summary> Represents a node in the three-dimensional world space. </summary>
    public class Node3d : Object3d
    {
        ///<summary> Represents the node's X-position in the 3D world. </summary>
        public readonly KeyframedValue<double> PositionX = new(InterpolatingFunctions.Double);

        ///<summary> Represents the node's Y-position in the 3D world. </summary>
        public readonly KeyframedValue<double> PositionY = new(InterpolatingFunctions.Double);

        ///<summary> Represents the node's Z-position in the 3D world. </summary>
        public readonly KeyframedValue<double> PositionZ = new(InterpolatingFunctions.Double);

        ///<summary> Represents the node's relative X-scale of children objects. </summary>
        public readonly KeyframedValue<double> ScaleX = new(InterpolatingFunctions.Double, 1);

        ///<summary> Represents the node's relative Y-scale of children objects. </summary>
        public readonly KeyframedValue<double> ScaleY = new(InterpolatingFunctions.Double, 1);

        ///<summary> Represents the node's relative Z-scale of children objects. </summary>
        public readonly KeyframedValue<double> ScaleZ = new(InterpolatingFunctions.Double, 1);

        ///<summary> Represents the node's quaternion rotation about the origin. </summary>
        public readonly KeyframedValue<Quaternion> Rotation = new(InterpolatingFunctions.QuaternionSlerp, Quaternion.Identity);

        ///<inheritdoc/>
        public override Matrix4x4 WorldTransformAt(double time) 
            => Matrix4x4.CreateScale((float)ScaleX.ValueAt(time), (float)ScaleY.ValueAt(time), (float)ScaleZ.ValueAt(time)) *
            Matrix4x4.CreateFromQuaternion(Rotation.ValueAt(time)) *
            Matrix4x4.CreateTranslation((float)PositionX.ValueAt(time), (float)PositionY.ValueAt(time), (float)PositionZ.ValueAt(time));
    }
}