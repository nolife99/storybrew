using osuTK;
using System;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

// osuTK 4.2
namespace StorybrewCommon.osuTKUtil
{
    ///<summary> Represents a 2D vector using two 32-bit integer numbers. </summary>
    ///<remarks> The Vector2i structure is suitable for interoperation with unmanaged code requiring two consecutive integers. </remarks>
    [Serializable][StructLayout(LayoutKind.Sequential)] public struct Vector2i : IEquatable<Vector2i> 
    {
        // Original: https://github.com/osuTK/osuTK/blob/master/src/osuTK.Mathematics/Vector/Vector2i.cs
        
        ///<summary> The X component of the Vector2i. </summary>
        public int X;

        ///<summary> The Y component of the Vector2i. </summary>
        public int Y;

        ///<summary> Initializes a new instance of the <see cref="Vector2i"/> struct. </summary>
        ///<param name="value">The value that will initialize this instance.</param>
        public Vector2i(int value)
        {
            X = value;
            Y = value;
        }

        ///<summary> Initializes a new instance of the <see cref="Vector2i"/> struct. </summary>
        ///<param name="x">The X component of the <see cref="Vector2i"/>.</param>
        ///<param name="y">The Y component of the <see cref="Vector2i"/>.</param>
        public Vector2i(int x, int y)
        {
            X = x;
            Y = y;
        }

        ///<summary> Gets or sets the value at the index of the vector. </summary>
        ///<param name="index">The index of the component from the vector.</param>
        ///<exception cref="IndexOutOfRangeException">Thrown if the index is less than 0 or greater than 1.</exception>
        public int this[int index]
        {
            readonly get
            {
                return index switch
                {
                    0 => X,
                    1 => Y,
                    _ => throw new IndexOutOfRangeException("You tried to access this vector at index: " + index),
                };
            }
            set
            {
                switch (index)
                {
                    case 0: X = value; return;
                    case 1: Y = value; return;
                    default: throw new IndexOutOfRangeException("You tried to access this vector at index: " + index);
                }
            }
        }

        ///<summary> Gets the manhattan length of the vector. </summary>
        public readonly int ManhattanLength => Math.Abs(X) + Math.Abs(Y);

        ///<summary> Gets the euclidian length of the vector. </summary>
        public readonly float EuclideanLength => (float)Math.Sqrt(X * X + Y * Y);

        ///<summary> Gets the perpendicular vector on the right side of this vector. </summary>
        public readonly Vector2i PerpendicularRight => new(Y, -X);

        ///<summary> Gets the perpendicular vector on the left side of this vector. </summary>
        public readonly Vector2i PerpendicularLeft => new(-Y, X);

        ///<summary> Defines a unit-length <see cref="Vector2i"/> that points towards the X-axis. </summary>
        public static readonly Vector2i UnitX = new(1, 0);

        ///<summary> Defines a unit-length <see cref="Vector2i"/> that points towards the Y-axis. </summary>
        public static readonly Vector2i UnitY = new(0, 1);

        ///<summary> Defines an instance with all components set to 0. </summary>
        public static readonly Vector2i Zero = new(0, 0);

        ///<summary> Defines an instance with all components set to 1. </summary>
        public static readonly Vector2i One = new(1, 1);

        ///<summary> Adds two vectors. </summary>
        ///<param name="a">Left operand.</param>
        ///<param name="b">Right operand.</param>
        ///<returns>The result of the operation.</returns>
        [Pure] public static Vector2i Add(Vector2i a, Vector2i b)
        {
            Add(in a, in b, out a);
            return a;
        }

        ///<summary> Adds two vectors. </summary>
        ///<param name="a">Left operand.</param>
        ///<param name="b">Right operand.</param>
        ///<param name="result">The result of the operation.</param>
        public static void Add(in Vector2i a, in Vector2i b, out Vector2i result)
        {
            result.X = a.X + b.X;
            result.Y = a.Y + b.Y;
        }

        ///<summary> Subtract one vector from another. </summary>
        ///<param name="a">First operand.</param>
        ///<param name="b">Second operand.</param>
        ///<returns>The result of the operation.</returns>
        [Pure] public static Vector2i Subtract(Vector2i a, Vector2i b)
        {
            Subtract(in a, in b, out a);
            return a;
        }

        ///<summary> Subtract one vector from another. </summary>
        ///<param name="a">First operand.</param>
        ///<param name="b">Second operand.</param>
        ///<param name="result">The result of the operation.</param>
        public static void Subtract(in Vector2i a, in Vector2i b, out Vector2i result)
        {
            result.X = a.X - b.X;
            result.Y = a.Y - b.Y;
        }

        ///<summary> Multiplies a vector by an integer scalar. </summary>
        ///<param name="vector">First operand.</param>
        ///<param name="scale">Second operand.</param>
        ///<returns>The result of the operation.</returns>
        [Pure] public static Vector2i Multiply(Vector2i vector, int scale)
        {
            Multiply(in vector, scale, out vector);
            return vector;
        }

        ///<summary> Multiplies a vector by an integer scalar. </summary>
        ///<param name="vector">First operand.</param>
        ///<param name="scale">Second operand.</param>
        ///<param name="result">The result of the operation.</param>
        public static void Multiply(in Vector2i vector, int scale, out Vector2i result)
        {
            result.X = vector.X * scale;
            result.Y = vector.Y * scale;
        }

        ///<summary> Multiplies a vector by the components a vector (scale). </summary>
        ///<param name="vector">First operand.</param>
        ///<param name="scale">Second operand.</param>
        ///<returns>The result of the operation.</returns>
        [Pure] public static Vector2i Multiply(Vector2i vector, Vector2i scale)
        {
            Multiply(in vector, in scale, out vector);
            return vector;
        }

        ///<summary> Multiplies a vector by the components of a vector (scale). </summary>
        ///<param name="vector">First operand.</param>
        ///<param name="scale">Second operand.</param>
        ///<param name="result">The result of the operation.</param>
        public static void Multiply(in Vector2i vector, in Vector2i scale, out Vector2i result)
        {
            result.X = vector.X * scale.X;
            result.Y = vector.Y * scale.Y;
        }

        ///<summary> Divides a vector by a scalar using integer division, floor(a/b). </summary>
        ///<param name="vector">First operand.</param>
        ///<param name="scale">Second operand.</param>
        ///<returns>The result of the operation.</returns>
        [Pure] public static Vector2i Divide(Vector2i vector, int scale)
        {
            Divide(in vector, scale, out vector);
            return vector;
        }

        ///<summary> Divides a vector by a scalar using integer division, floor(a/b). </summary>
        ///<param name="vector">First operand.</param>
        ///<param name="scale">Second operand.</param>
        ///<param name="result">The result of the operation.</param>
        public static void Divide(in Vector2i vector, int scale, out Vector2i result)
        {
            result.X = vector.X / scale;
            result.Y = vector.Y / scale;
        }

        ///<summary> Divides a vector by the components of a vector using integer division, floor(a/b). </summary>
        ///<param name="vector">First operand.</param>
        ///<param name="scale">Second operand.</param>
        ///<returns>The result of the operation.</returns>
        [Pure] public static Vector2i Divide(Vector2i vector, Vector2i scale)
        {
            Divide(in vector, in scale, out vector);
            return vector;
        }

        ///<summary> Divides a vector by the components of a vector using integer division, floor(a/b). </summary>
        ///<param name="vector">First operand.</param>
        ///<param name="scale">Second operand.</param>
        ///<param name="result">The result of the operation.</param>
        public static void Divide(in Vector2i vector, in Vector2i scale, out Vector2i result)
        {
            result.X = vector.X / scale.X;
            result.Y = vector.Y / scale.Y;
        }

        ///<summary> Returns a vector created from the smallest of the corresponding components of the given vectors. </summary>
        ///<param name="a">First operand.</param>
        ///<param name="b">Second operand.</param>
        ///<returns>The component-wise minimum.</returns>
        [Pure] public static Vector2i ComponentMin(Vector2i a, Vector2i b)
        {
            a.X = Math.Min(a.X, b.X);
            a.Y = Math.Min(a.Y, b.Y);
            return a;
        }

        /// <summary>
        /// Returns a vector created from the smallest of the corresponding components of the given vectors.
        /// </summary>
        /// <param name="a">First operand.</param>
        /// <param name="b">Second operand.</param>
        /// <param name="result">The component-wise minimum.</param>
        public static void ComponentMin(in Vector2i a, in Vector2i b, out Vector2i result)
        {
            result.X = Math.Min(a.X, b.X);
            result.Y = Math.Min(a.Y, b.Y);
        }

        ///<summary> Returns a vector created from the largest of the corresponding components of the given vectors. </summary>
        ///<param name="a">First operand.</param>
        ///<param name="b">Second operand.</param>
        ///<returns>The component-wise minimum.</returns>
        [Pure] public static Vector2i ComponentMax(Vector2i a, Vector2i b)
        {
            a.X = Math.Max(a.X, b.X);
            a.Y = Math.Max(a.Y, b.Y);
            return a;
        }

        ///<summary> Returns a vector created from the largest of the corresponding components of the given vectors. </summary>
        ///<param name="a">First operand.</param>
        ///<param name="b">Second operand.</param>
        ///<param name="result">The component-wise maximum.</param>
        public static void ComponentMax(in Vector2i a, in Vector2i b, out Vector2i result)
        {
            result.X = Math.Max(a.X, b.X);
            result.Y = Math.Max(a.Y, b.Y);
        }

        ///<summary> Clamp a vector to the given minimum and maximum vectors. </summary>
        ///<param name="vec">Input vector.</param>
        ///<param name="min">Minimum vector.</param>
        ///<param name="max">Maximum vector.</param>
        ///<returns>The clamped vector.</returns>
        [Pure] public static Vector2i Clamp(Vector2i vec, Vector2i min, Vector2i max)
        {
            vec.X = MathHelper.Clamp(vec.X, min.X, max.X);
            vec.Y = MathHelper.Clamp(vec.Y, min.Y, max.Y);
            return vec;
        }

        ///<summary> Clamp a vector to the given minimum and maximum vectors. </summary>
        ///<param name="vec">Input vector.</param>
        ///<param name="min">Minimum vector.</param>
        ///<param name="max">Maximum vector.</param>
        ///<param name="result">The clamped vector.</param>
        public static void Clamp(in Vector2i vec, in Vector2i min, in Vector2i max, out Vector2i result)
        {
            result.X = MathHelper.Clamp(vec.X, min.X, max.X);
            result.Y = MathHelper.Clamp(vec.Y, min.Y, max.Y);
        }

        ///<summary> Gets or sets a <see cref="Vector2i"/> with the Y and X components of this instance. </summary>
        [XmlIgnore] public Vector2i Yx
        {
            readonly get => new(Y, X);
            set
            {
                Y = value.X;
                X = value.Y;
            }
        }

        ///<summary> Gets a <see cref="Vector2"/> object with the same component values as the <see cref="Vector2i"/> instance. </summary>
        ///<returns>The resulting <see cref="Vector3"/> instance.</returns>
        public readonly Vector2 ToVector2() => new(X, Y);

        ///<summary> Gets a <see cref="Vector2"/> object with the same component values as the <see cref="Vector2i"/> instance. </summary>
        ///<param name="input">The given <see cref="Vector2i"/> to convert.</param>
        ///<param name="result">The resulting <see cref="Vector2"/>.</param>
        public static void ToVector2(in Vector2i input, out Vector2 result)
        {
            result.X = input.X;
            result.Y = input.Y;
        }

        ///<summary> Adds the specified instances. </summary>
        ///<param name="left">Left operand.</param>
        ///<param name="right">Right operand.</param>
        ///<returns>Result of addition.</returns>
        [Pure] public static Vector2i operator +(Vector2i left, Vector2i right) => Add(left, right);

        ///<summary> Subtracts the specified instances. </summary>
        ///<param name="left">Left operand.</param>
        ///<param name="right">Right operand.</param>
        ///<returns>Result of subtraction.</returns>
        [Pure] public static Vector2i operator -(Vector2i left, Vector2i right) => Subtract(left, right);

        ///<summary> Negates the specified instance. </summary>
        ///<param name="vec">Operand.</param>
        ///<returns>Result of negation.</returns>
        [Pure] public static Vector2i operator -(Vector2i vec)
        {
            vec.X = -vec.X;
            vec.Y = -vec.Y;
            return vec;
        }

        ///<summary> Multiplies the specified instance by a scalar. </summary>
        ///<param name="vec">Left operand.</param>
        ///<param name="scale">Right operand.</param>
        ///<returns>Result of multiplication.</returns>
        [Pure] public static Vector2i operator *(Vector2i vec, int scale) => Multiply(vec, scale);

        ///<summary> Multiplies the specified instance by a scalar. </summary>
        ///<param name="vec">Left operand.</param>
        ///<param name="scale">Right operand.</param>
        ///<returns>Result of multiplication.</returns>
        [Pure] public static Vector2i operator *(int scale, Vector2i vec) => Multiply(vec, scale);

        ///<summary> Component-wise multiplication between the specified instance by a scale vector. </summary>
        ///<param name="scale">Left operand.</param>
        ///<param name="vec">Right operand.</param>
        ///<returns>Result of multiplication.</returns>
        [Pure] public static Vector2i operator *(Vector2i vec, Vector2i scale) => Multiply(vec, scale);

        ///<summary> Divides the instance by a scalar using integer division, floor(a/b). </summary>
        ///<param name="vec">Left operand.</param>
        ///<param name="scale">Right operand.</param>
        ///<returns>Result of the division.</returns>
        [Pure] public static Vector2i operator /(Vector2i vec, int scale) => Divide(vec, scale);

        ///<summary> Component-wise division between the specified instance by a scale vector. </summary>
        ///<param name="vec">Left operand.</param>
        ///<param name="scale">Right operand.</param>
        ///<returns>Result of the division.</returns>
        [Pure] public static Vector2i operator /(Vector2i vec, Vector2i scale) => Divide(vec, scale);

        ///<summary> Compares the specified instances for equality. </summary>
        ///<param name="left">Left operand.</param>
        ///<param name="right">Right operand.</param>
        ///<returns>True if both instances are equal; false otherwise.</returns>
        public static bool operator ==(Vector2i left, Vector2i right) => left.Equals(right);

        ///<summary> Compares the specified instances for inequality. </summary>
        ///<param name="left">Left operand.</param>
        ///<param name="right">Right operand.</param>
        ///<returns>True if both instances are not equal; false otherwise.</returns>
        public static bool operator !=(Vector2i left, Vector2i right) => !(left == right);

        ///<summary/>
        [Pure] public static implicit operator Vector2(Vector2i vec) => vec.ToVector2();

        ///<summary/>
        [Pure] public static implicit operator Vector2d(Vector2i vec) => new(vec.X, vec.Y);

        ///<summary/>
        [Pure] public static explicit operator Vector2h(Vector2i vec) => new(vec.X, vec.Y);

        ///<summary/>
        [Pure] public static implicit operator Vector2i((int X, int Y) values) => new(values.X, values.Y);

        ///<inheritdoc/>
        public override bool Equals(object obj) => obj is Vector2i i && Equals(i);

        ///<inheritdoc/>
        public readonly bool Equals(Vector2i other) => X == other.X && Y == other.Y;

        ///<inheritdoc/>
        public override readonly int GetHashCode() => base.GetHashCode();

        ///<summary> Deconstructs the vector into its individual components. </summary>
        ///<param name="x">The X component of the vector.</param>
        ///<param name="y">The Y component of the vector.</param>
        [Pure] public readonly void Deconstruct(out int x, out int y)
        {
            x = X;
            y = Y;
        }
    }

    ///<summary> Represents a 3D vector using three 32-bit integer numbers. </summary>
    ///<remarks> The Vector3i structure is suitable for interoperation with unmanaged code requiring three consecutive integers. </remarks>
    [Serializable] [StructLayout(LayoutKind.Sequential)] public struct Vector3i : IEquatable<Vector3i>
    {
        // Original: https://github.com/osuTK/osuTK/blob/master/src/osuTK.Mathematics/Vector/Vector3i.cs
        
        ///<summary> The X component of the Vector3i. </summary>
        public int X;

        ///<summary> The Y component of the Vector3i. </summary>
        public int Y;

        ///<summary> The Z component of the Vector3i. </summary>
        public int Z;

        ///<summary> Initializes a new instance of the <see cref="Vector3i"/> struct. </summary>
        ///<param name="value">The value that will initialize this instance.</param>
        public Vector3i(int value)
        {
            X = value;
            Y = value;
            Z = value;
        }

        ///<summary> Initializes a new instance of the <see cref="Vector3i"/> struct. </summary>
        ///<param name="x">The x component of the Vector3.</param>
        ///<param name="y">The y component of the Vector3.</param>
        ///<param name="z">The z component of the Vector3.</param>
        public Vector3i(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        ///<summary> Initializes a new instance of the <see cref="Vector3i"/> struct. </summary>
        ///<param name="v">The <see cref="Vector2i"/> to copy components from.</param>
        public Vector3i(Vector2i v)
        {
            X = v.X;
            Y = v.Y;
            Z = 0;
        }

        ///<summary> Initializes a new instance of the <see cref="Vector3i"/> struct. </summary>
        ///<param name="v">The <see cref="Vector2i"/> to copy components from.</param>
        ///<param name="z">The z component of the new Vector3.</param>
        public Vector3i(Vector2i v, int z)
        {
            X = v.X;
            Y = v.Y;
            Z = z;
        }

        ///<summary> Gets or sets the value at the index of the vector. </summary>
        ///<param name="index">The index of the component from the vector.</param>
        ///<exception cref="IndexOutOfRangeException">Thrown if the index is less than 0 or greater than 2.</exception>
        public int this[int index]
        {
            readonly get
            {
                return index switch
                {
                    0 => X,
                    1 => Y,
                    2 => Z,
                    _ => throw new IndexOutOfRangeException("You tried to access this vector at index: " + index),
                };
            }
            set
            {
                switch (index)
                {
                    case 0: X = value; return;
                    case 1: Y = value; return;
                    case 2: Z = value; return;
                    default: throw new IndexOutOfRangeException("You tried to set this vector at index: " + index);
                }
            }
        }

        ///<summary> Gets the manhattan length of the vector. </summary>
        public readonly int ManhattanLength => Math.Abs(X) + Math.Abs(Y) + Math.Abs(Z);

        ///<summary> Gets the euclidian length of the vector. </summary>
        public readonly float EuclideanLength => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

        ///<summary> Defines a unit-length Vector3i that points towards the X-axis. </summary>
        public static readonly Vector3i UnitX = new(1, 0, 0);

        ///<summary> Defines a unit-length Vector3i that points towards the Y-axis. </summary>
        public static readonly Vector3i UnitY = new(0, 1, 0);

        ///<summary> Defines a unit-length Vector3i that points towards the Z-axis. </summary>
        public static readonly Vector3i UnitZ = new(0, 0, 1);

        ///<summary> Defines an instance with all components set to 0. </summary>
        public static readonly Vector3i Zero = new(0, 0, 0);

        ///<summary> Defines an instance with all components set to 1. </summary>
        public static readonly Vector3i One = new(1, 1, 1);

        ///<summary> Adds two vectors. </summary>
        ///<param name="a">Left operand.</param>
        ///<param name="b">Right operand.</param>
        ///<returns>Result of operation.</returns>
        [Pure] public static Vector3i Add(Vector3i a, Vector3i b)
        {
            Add(in a, in b, out a);
            return a;
        }

        ///<summary> Adds two vectors. </summary>
        ///<param name="a">Left operand.</param>
        ///<param name="b">Right operand.</param>
        ///<param name="result">The result of the operation.</param>
        public static void Add(in Vector3i a, in Vector3i b, out Vector3i result)
        {
            result.X = a.X + b.X;
            result.Y = a.Y + b.Y;
            result.Z = a.Z + b.Z;
        }

        ///<summary> Subtract one vector from another. </summary>
        ///<param name="a">First operand.</param>
        ///<param name="b">Second operand.</param>
        ///<returns>Result of subtraction.</returns>
        [Pure] public static Vector3i Subtract(Vector3i a, Vector3i b)
        {
            Subtract(in a, in b, out a);
            return a;
        }

        ///<summary> Subtract one vector from another. </summary>
        ///<param name="a">First operand.</param>
        ///<param name="b">Second operand.</param>
        ///<param name="result">The result of the operation.</param>
        public static void Subtract(in Vector3i a, in Vector3i b, out Vector3i result)
        {
            result.X = a.X - b.X;
            result.Y = a.Y - b.Y;
            result.Z = a.Z - b.Z;
        }

        ///<summary> Multiplies a vector by a scalar. </summary>
        ///<param name="vector">Left operand.</param>
        ///<param name="scale">Right operand.</param>
        ///<returns>Result of the operation.</returns>
        [Pure] public static Vector3i Multiply(Vector3i vector, int scale)
        {
            Multiply(in vector, scale, out vector);
            return vector;
        }

        ///<summary> Multiplies a vector by a scalar. </summary>
        ///<param name="vector">Left operand.</param>
        ///<param name="scale">Right operand.</param>
        ///<param name="result">The result of the operation.</param>
        public static void Multiply(in Vector3i vector, int scale, out Vector3i result)
        {
            result.X = vector.X * scale;
            result.Y = vector.Y * scale;
            result.Z = vector.Z * scale;
        }

        ///<summary> Multiplies a vector by the components of a vector (scale). </summary>
        ///<param name="vector">Left operand.</param>
        ///<param name="scale">Right operand.</param>
        ///<returns>Result of the operation.</returns>
        [Pure] public static Vector3i Multiply(Vector3i vector, Vector3i scale)
        {
            Multiply(in vector, in scale, out vector);
            return vector;
        }

        ///<summary> Multiplies a vector by the components of a vector (scale). </summary>
        ///<param name="vector">Left operand.</param>
        ///<param name="scale">Right operand.</param>
        ///<param name="result">The result of the operation.</param>
        public static void Multiply(in Vector3i vector, in Vector3i scale, out Vector3i result)
        {
            result.X = vector.X * scale.X;
            result.Y = vector.Y * scale.Y;
            result.Z = vector.Z * scale.Z;
        }

        ///<summary> Divides a vector by a scalar using integer division, floor(a/b). </summary>
        ///<param name="vector">Left operand.</param>
        ///<param name="scale">Right operand.</param>
        ///<returns>Result of the operation.</returns>
        [Pure] public static Vector3i Divide(Vector3i vector, int scale)
        {
            Divide(in vector, scale, out vector);
            return vector;
        }

        ///<summary> Divides a vector by a scalar using integer division, floor(a/b). </summary>
        ///<param name="vector">Left operand.</param>
        ///<param name="scale">Right operand.</param>
        ///<param name="result">The result of the operation.</param>
        public static void Divide(in Vector3i vector, int scale, out Vector3i result)
        {
            result.X = vector.X / scale;
            result.Y = vector.Y / scale;
            result.Z = vector.Z / scale;
        }

        ///<summary> Divides a vector by the components of a vector using integer division, floor(a/b). </summary>
        ///<param name="vector">Left operand.</param>
        ///<param name="scale">Right operand.</param>
        ///<returns>Result of the operation.</returns>
        [Pure] public static Vector3i Divide(Vector3i vector, Vector3i scale)
        {
            Divide(in vector, in scale, out vector);
            return vector;
        }

        ///<summary> Divides a vector by the components of a vector using integer division, floor(a/b). </summary>
        ///<param name="vector">Left operand.</param>
        ///<param name="scale">Right operand.</param>
        ///<param name="result">The result of the operation.</param>
        public static void Divide(in Vector3i vector, in Vector3i scale, out Vector3i result)
        {
            result.X = vector.X / scale.X;
            result.Y = vector.Y / scale.Y;
            result.Z = vector.Z / scale.Z;
        }

        ///<summary> Returns a vector created from the smallest of the corresponding components of the given vectors. </summary>
        ///<param name="a">First operand.</param>
        ///<param name="b">Second operand.</param>
        ///<returns>The component-wise minimum.</returns>
        [Pure] public static Vector3i ComponentMin(Vector3i a, Vector3i b)
        {
            Vector3i result;
            result.X = Math.Min(a.X, b.X);
            result.Y = Math.Min(a.Y, b.Y);
            result.Z = Math.Min(a.Z, b.Z);
            return result;
        }

        ///<summary> Returns a vector created from the smallest of the corresponding components of the given vectors. </summary>
        ///<param name="a">First operand.</param>
        ///<param name="b">Second operand.</param>
        ///<param name="result">The component-wise minimum.</param>
        public static void ComponentMin(in Vector3i a, in Vector3i b, out Vector3i result)
        {
            result.X = Math.Min(a.X, b.X);
            result.Y = Math.Min(a.Y, b.Y);
            result.Z = Math.Min(a.Z, b.Z);
        }

        ///<summary> Returns a vector created from the largest of the corresponding components of the given vectors. </summary>
        ///<param name="a">First operand.</param>
        ///<param name="b">Second operand.</param>
        ///<returns>The component-wise maximum.</returns>
        [Pure] public static Vector3i ComponentMax(Vector3i a, Vector3i b)
        {
            Vector3i result;
            result.X = Math.Max(a.X, b.X);
            result.Y = Math.Max(a.Y, b.Y);
            result.Z = Math.Max(a.Z, b.Z);
            return result;
        }

        ///<summary> Returns a vector created from the largest of the corresponding components of the given vectors. </summary>
        ///<param name="a">First operand.</param>
        ///<param name="b">Second operand.</param>
        ///<param name="result">The component-wise maximum.</param>
        public static void ComponentMax(in Vector3i a, in Vector3i b, out Vector3i result)
        {
            result.X = Math.Max(a.X, b.X);
            result.Y = Math.Max(a.Y, b.Y);
            result.Z = Math.Max(a.Z, b.Z);
        }

        ///<summary> Clamps a vector to the given minimum and maximum vectors. </summary>
        ///<param name="vec">Input vector.</param>
        ///<param name="min">Minimum vector.</param>
        ///<param name="max">Maximum vector.</param>
        ///<returns>The clamped vector.</returns>
        [Pure] public static Vector3i Clamp(Vector3i vec, Vector3i min, Vector3i max)
        {
            Vector3i result;
            result.X = MathHelper.Clamp(vec.X, min.X, max.X);
            result.Y = MathHelper.Clamp(vec.Y, min.Y, max.Y);
            result.Z = MathHelper.Clamp(vec.Z, min.Z, max.Z);
            return result;
        }

        ///<summary> Clamps a vector to the given minimum and maximum vectors. </summary>
        ///<param name="vec">Input vector.</param>
        ///<param name="min">Minimum vector.</param>
        ///<param name="max">Maximum vector.</param>
        ///<param name="result">The clamped vector.</param>
        public static void Clamp(in Vector3i vec, in Vector3i min, in Vector3i max, out Vector3i result)
        {
            result.X = MathHelper.Clamp(vec.X, min.X, max.X);
            result.Y = MathHelper.Clamp(vec.Y, min.Y, max.Y);
            result.Z = MathHelper.Clamp(vec.Z, min.Z, max.Z);
        }

        ///<summary> Gets or sets a <see cref="Vector2i"/> with the X and Y components of this instance. </summary>
        [XmlIgnore] public Vector2i Xy
        {
            readonly get => new(X, Y);
            set
            {
                X = value.X;
                Y = value.Y;
            }
        }

        ///<summary> Gets or sets a <see cref="Vector2i"/> with the X and Z components of this instance. </summary>
        [XmlIgnore] public Vector2i Xz
        {
            readonly get => new(X, Z);
            set
            {
                X = value.X;
                Z = value.Y;
            }
        }

        ///<summary> Gets or sets a <see cref="Vector2i"/> with the Y and X components of this instance. </summary>
        [XmlIgnore] public Vector2i Yx
        {
            readonly get => new(Y, X);
            set
            {
                Y = value.X;
                X = value.Y;
            }
        }

        ///<summary> Gets or sets a <see cref="Vector2i"/> with the Y and Z components of this instance. </summary>
        [XmlIgnore] public Vector2i Yz
        {
            readonly get => new(Y, Z);
            set
            {
                Y = value.X;
                Z = value.Y;
            }
        }

        ///<summary> Gets or sets a <see cref="Vector2i"/> with the Z and X components of this instance. </summary>
        [XmlIgnore] public Vector2i Zx
        {
            readonly get => new(Z, X);
            set
            {
                Z = value.X;
                X = value.Y;
            }
        }

        ///<summary> Gets or sets a <see cref="Vector2i"/> with the Z and Y components of this instance. </summary>
        [XmlIgnore] public Vector2i Zy
        {
            readonly get => new(Z, Y);
            set
            {
                Z = value.X;
                Y = value.Y;
            }
        }

        ///<summary> Gets or sets a <see cref="Vector3i"/> with the X, Z, and Y components of this instance. </summary>
        [XmlIgnore] public Vector3i Xzy
        {
            readonly get => new(X, Z, Y);
            set
            {
                X = value.X;
                Z = value.Y;
                Y = value.Z;
            }
        }

        ///<summary> Gets or sets a <see cref="Vector3i"/> with the Y, X, and Z components of this instance. </summary>
        [XmlIgnore] public Vector3i Yxz
        {
            readonly get => new(Y, X, Z);
            set
            {
                Y = value.X;
                X = value.Y;
                Z = value.Z;
            }
        }

        ///<summary> Gets or sets a <see cref="Vector3i"/> with the Y, Z, and X components of this instance. </summary>
        [XmlIgnore] public Vector3i Yzx
        {
            readonly get => new(Y, Z, X);
            set
            {
                Y = value.X;
                Z = value.Y;
                X = value.Z;
            }
        }

        ///<summary> Gets or sets a <see cref="Vector3i"/> with the Z, X, and Y components of this instance. </summary>
        [XmlIgnore] public Vector3i Zxy
        {
            readonly get => new(Z, X, Y);
            set
            {
                Z = value.X;
                X = value.Y;
                Y = value.Z;
            }
        }

        ///<summary> Gets or sets a <see cref="Vector3i"/> with the Z, Y, and X components of this instance. </summary>
        [XmlIgnore] public Vector3i Zyx
        {
            readonly get => new(Z, Y, X);
            set
            {
                Z = value.X;
                Y = value.Y;
                X = value.Z;
            }
        }

        ///<summary> Gets a <see cref="Vector3"/> object with the same component values as the <see cref="Vector3i"/> instance. </summary>
        ///<returns>The resulting <see cref="Vector3"/> instance.</returns>
        public readonly Vector3 ToVector3() => new(X, Y, Z);

        ///<summary> Gets a <see cref="Vector3"/> object with the same component values as the <see cref="Vector3i"/> instance. </summary>
        ///<param name="input">The given <see cref="Vector3i"/> to convert.</param>
        ///<param name="result">The resulting <see cref="Vector3"/>.</param>
        public static void ToVector3(in Vector3i input, out Vector3 result)
        {
            result.X = input.X;
            result.Y = input.Y;
            result.Z = input.Z;
        }

        ///<summary> Adds two instances. </summary>
        ///<param name="left">The first instance.</param>
        ///<param name="right">The second instance.</param>
        ///<returns>The result of the calculation.</returns>
        [Pure] public static Vector3i operator +(Vector3i left, Vector3i right) => Add(left, right);

        ///<summary> Subtracts two instances. </summary>
        ///<param name="left">The first instance.</param>
        ///<param name="right">The second instance.</param>
        ///<returns>The result of the calculation.</returns>
        [Pure] public static Vector3i operator -(Vector3i left, Vector3i right) => Subtract(left, right);

        ///<summary> Negates an instance. </summary>
        ///<param name="vec">The instance.</param>
        ///<returns>The result of the calculation.</returns>
        [Pure] public static Vector3i operator -(Vector3i vec)
        {
            vec.X = -vec.X;
            vec.Y = -vec.Y;
            vec.Z = -vec.Z;
            return vec;
        }

        ///<summary> Multiplies an instance by an integer scalar. </summary>
        ///<param name="vec">The instance.</param>
        ///<param name="scale">The scalar.</param>
        ///<returns>The result of the calculation.</returns>
        [Pure] public static Vector3i operator *(Vector3i vec, int scale) => Multiply(vec, scale);

        ///<summary> Multiplies an instance by an integer scalar. </summary>
        ///<param name="vec">The instance.</param>
        ///<param name="scale">The scalar.</param>
        ///<returns>The result of the calculation.</returns>
        [Pure] public static Vector3i operator *(int scale, Vector3i vec) => Multiply(vec, scale);

        ///<summary> Component-wise multiplication between the specified instance by a scale vector. </summary>
        ///<param name="scale">Left operand.</param>
        ///<param name="vec">Right operand.</param>
        ///<returns>Result of multiplication.</returns>
        [Pure] public static Vector3i operator *(Vector3i vec, Vector3i scale) => Multiply(vec, scale);

        ///<summary> Divides the instance by a scalar using integer division, floor(a/b). </summary>
        ///<param name="vec">The instance.</param>
        ///<param name="scale">The scalar.</param>
        ///<returns>The result of the calculation.</returns>
        [Pure] public static Vector3i operator /(Vector3i vec, int scale) => Divide(vec, scale);

        ///<summary> Component-wise division between the specified instance by a scale vector. </summary>
        ///<param name="vec">Left operand.</param>
        ///<param name="scale">Right operand.</param>
        ///<returns>Result of the division.</returns>
        [Pure] public static Vector3i operator /(Vector3i vec, Vector3i scale) => Divide(vec, scale);

        ///<summary> Compares two instances for equality. </summary>
        ///<param name="left">The first instance.</param>
        ///<param name="right">The second instance.</param>
        ///<returns>True, if left equals right; false otherwise.</returns>
        public static bool operator ==(Vector3i left, Vector3i right) => left.Equals(right);

        ///<summary> Compares two instances for inequality. </summary>
        ///<param name="left">The first instance.</param>
        ///<param name="right">The second instance.</param>
        ///<returns>True, if left does not equal right; false otherwise.</returns>
        public static bool operator !=(Vector3i left, Vector3i right) => !(left == right);

        ///<summary/>
        [Pure] public static implicit operator Vector3(Vector3i vec) => new(vec.X, vec.Y, vec.Z);

        ///<summary/>
        [Pure] public static implicit operator Vector3d(Vector3i vec) => new(vec.X, vec.Y, vec.Z);

        ///<summary/>
        [Pure] public static explicit operator Vector3h(Vector3i vec) => new(vec.X, vec.Y, vec.Z);

        ///<summary/>
        [Pure] public static implicit operator Vector3i((int X, int Y, int Z) values) => new(values.X, values.Y, values.Z);

        ///<inheritdoc/>
        public override bool Equals(object obj) => obj is Vector3i i && Equals(i);

        ///<inheritdoc/>
        public readonly bool Equals(Vector3i other) => X == other.X && Y == other.Y && Z == other.Z;

        ///<inheritdoc/>
        public override readonly int GetHashCode() => base.GetHashCode();

        ///<summary> Deconstructs the vector into its individual components. </summary>
        ///<param name="x">The X component of the vector.</param>
        ///<param name="y">The Y component of the vector.</param>
        ///<param name="z">The Z component of the vector.</param>
        [Pure] public readonly void Deconstruct(out int x, out int y, out int z)
        {
            x = X;
            y = Y;
            z = Z;
        }
    }
}