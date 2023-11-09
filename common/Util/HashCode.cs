using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace StorybrewCommon.Util
{
    ///<summary> Combines the hash code for multiple values into a single hash code. </summary>
    ///<remarks> 
    ///  <see cref="HashCode"/> is used to combine multiple values (for example, fields on a structure or class) into a single hash code. 
    ///  This structure operates in one of two ways:
    ///  <list type="bullet">
    ///    <item>
    ///      <term> Static Methods </term>
    ///      <description> Combine the hash codes of up to eight values. </description>
    ///    </item>
    ///    <item>
    ///      <term> Instance Methods </term>
    ///      <description> Two methods (<see cref="Add{T}"/> and <see cref="ToHashCode"/>) operating in a streaming fashion, accepting values consecutively. </description>
    ///    </item>
    ///  </list>
    ///</remarks>
    public struct HashCode //.NET Core 3.1 implementation
    {
        static readonly uint s_seed = GenerateGlobalSeed();

        const uint prime1 = 0x9E3779B1, prime2 = 0x85EBCA77, prime3 = 0xC2B2AE3D, prime4 = 0x27D4EB2F, prime5 = 0x165667B1;
        uint _v1, _v2, _v3, _v4, _queue1, _queue2, _queue3, _length;

        ///<summary> Constructs a new hash combiner. </summary>
        ///<param name="hashes"> Currently-existing hash codes to input to the final combination. </param>
        public HashCode(params int[] hashes) : this()
        {
            for (var i = 0; i < hashes.Length; ++i) Add(hashes[i]);
        }

        [DllImport("bcrypt.dll", EntryPoint = "BCryptGenRandom", CallingConvention = CallingConvention.Winapi)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static extern unsafe uint CryptoRandomBytes(IntPtr hAlgorithm, byte* pbBuffer, int cbBuffer, int dwFlags);

        static unsafe uint GenerateGlobalSeed()
        {
            uint result;
            var err = CryptoRandomBytes(default, (byte*)&result, sizeof(uint), 0x00000002);
            if (err != 0) using (var rng = new RNGCryptoServiceProvider())
            {
                var bytes = new byte[sizeof(uint)];
                rng.GetBytes(bytes);
                
                fixed (byte* addr = &bytes[0]) result = *(uint*)addr;
            }

            return result;
        }

        ///<summary> Diffuses the hash code returned by the specified value. </summary>
        ///<typeparam name="T1"> The type of the value to add the hash code. </typeparam>
        ///<param name="value1"> The value to add to the hash code. </param>
        ///<returns> The hash code that represents the single value. </returns>
        public static int Combine<T1>(T1 value1)
        {
            uint hc1 = (uint)(value1?.GetHashCode() ?? 0), hash = MixEmptyState() + 4;
            hash = QueueRound(hash, hc1);

            return (int)MixFinal(hash);
        }

        ///<summary> Combines two values into a hash code. </summary>
        ///<typeparam name="T1"> The type of the first value to combine into the hash code. </typeparam>
        ///<typeparam name="T2"> The type of the second value to combine into the hash code. </typeparam>
        ///<param name="value1"> The first value to combine into the hash code. </param>
        ///<param name="value2"> The second value to combine into the hash code. </param>
        ///<returns> The hash code that represents the two values. </returns>
        public static int Combine<T1, T2>(T1 value1, T2 value2)
        {
            uint hc1 = (uint)(value1?.GetHashCode() ?? 0), hc2 = (uint)(value2?.GetHashCode() ?? 0), hash = MixEmptyState() + 8;
            hash = QueueRound(hash, hc1);
            hash = QueueRound(hash, hc2);

            return (int)MixFinal(hash);
        }

        ///<summary> Combines three values into a hash code. </summary>
        ///<typeparam name="T1"> The type of the first value to combine into the hash code. </typeparam>
        ///<typeparam name="T2"> The type of the second value to combine into the hash code. </typeparam>
        ///<typeparam name="T3"> The type of the third value to combine into the hash code. </typeparam>
        ///<param name="value1"> The first value to combine into the hash code. </param>
        ///<param name="value2"> The second value to combine into the hash code. </param>
        ///<param name="value3"> The third value to combine into the hash code. </param>
        ///<returns> The hash code that represents the three values. </returns>
        public static int Combine<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
        {
            uint hc1 = (uint)(value1?.GetHashCode() ?? 0), hc2 = (uint)(value2?.GetHashCode() ?? 0), hc3 = (uint)(value3?.GetHashCode() ?? 0), hash = MixEmptyState() + 12;
            hash = QueueRound(hash, hc1);
            hash = QueueRound(hash, hc2);
            hash = QueueRound(hash, hc3);

            return (int)MixFinal(hash);
        }

        ///<summary> Combines four values into a hash code. </summary>
        ///<typeparam name="T1"> The type of the first value to combine into the hash code. </typeparam>
        ///<typeparam name="T2"> The type of the second value to combine into the hash code. </typeparam>
        ///<typeparam name="T3"> The type of the third value to combine into the hash code. </typeparam>
        ///<typeparam name="T4"> The type of the fourth value to combine into the hash code. </typeparam>
        ///<param name="value1"> The first value to combine into the hash code. </param>
        ///<param name="value2"> The second value to combine into the hash code. </param>
        ///<param name="value3"> The third value to combine into the hash code. </param>
        ///<param name="value4"> The fourth value to combine into the hash code. </param>
        ///<returns> The hash code that represents the four values. </returns>
        public static int Combine<T1, T2, T3, T4>(T1 value1, T2 value2, T3 value3, T4 value4)
        {
            uint hc1 = (uint)(value1?.GetHashCode() ?? 0),
                hc2 = (uint)(value2?.GetHashCode() ?? 0),
                hc3 = (uint)(value3?.GetHashCode() ?? 0),
                hc4 = (uint)(value4?.GetHashCode() ?? 0);

            Initialize(out var v1, out var v2, out var v3, out var v4);

            v1 = Round(v1, hc1);
            v2 = Round(v2, hc2);
            v3 = Round(v3, hc3);
            v4 = Round(v4, hc4);

            var hash = MixState(v1, v2, v3, v4) + 16;

            return (int)MixFinal(hash);
        }

        ///<summary> Combines five values into a hash code. </summary>
        ///<typeparam name="T1"> The type of the first value to combine into the hash code. </typeparam>
        ///<typeparam name="T2"> The type of the second value to combine into the hash code. </typeparam>
        ///<typeparam name="T3"> The type of the third value to combine into the hash code. </typeparam>
        ///<typeparam name="T4"> The type of the fourth value to combine into the hash code. </typeparam>
        ///<typeparam name="T5"> The type of the fifth value to combine into the hash code. </typeparam>
        ///<param name="value1"> The first value to combine into the hash code. </param>
        ///<param name="value2"> The second value to combine into the hash code. </param>
        ///<param name="value3"> The third value to combine into the hash code. </param>
        ///<param name="value4"> The fourth value to combine into the hash code. </param>
        ///<param name="value5"> The fifth value to combine into the hash code. </param>
        ///<returns> The hash code that represents the five values. </returns>
        public static int Combine<T1, T2, T3, T4, T5>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5)
        {
            uint hc1 = (uint)(value1?.GetHashCode() ?? 0),
                hc2 = (uint)(value2?.GetHashCode() ?? 0),
                hc3 = (uint)(value3?.GetHashCode() ?? 0),
                hc4 = (uint)(value4?.GetHashCode() ?? 0),
                hc5 = (uint)(value5?.GetHashCode() ?? 0);

            Initialize(out var v1, out var v2, out var v3, out var v4);

            v1 = Round(v1, hc1);
            v2 = Round(v2, hc2);
            v3 = Round(v3, hc3);
            v4 = Round(v4, hc4);

            var hash = MixState(v1, v2, v3, v4) + 20;
            hash = QueueRound(hash, hc5);

            return (int)MixFinal(hash);
        }

        ///<summary> Combines six values into a hash code. </summary>
        ///<typeparam name="T1"> The type of the first value to combine into the hash code. </typeparam>
        ///<typeparam name="T2"> The type of the second value to combine into the hash code. </typeparam>
        ///<typeparam name="T3"> The type of the third value to combine into the hash code. </typeparam>
        ///<typeparam name="T4"> The type of the fourth value to combine into the hash code. </typeparam>
        ///<typeparam name="T5"> The type of the fifth value to combine into the hash code. </typeparam>
        ///<typeparam name="T6"> The type of the sixth value to combine into the hash code. </typeparam>
        ///<param name="value1"> The first value to combine into the hash code. </param>
        ///<param name="value2"> The second value to combine into the hash code. </param>
        ///<param name="value3"> The third value to combine into the hash code. </param>
        ///<param name="value4"> The fourth value to combine into the hash code. </param>
        ///<param name="value5"> The fifth value to combine into the hash code. </param>
        ///<param name="value6"> The sixth value to combine into the hash code. </param>
        ///<returns> The hash code that represents the six values. </returns>
        public static int Combine<T1, T2, T3, T4, T5, T6>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6)
        {
            uint hc1 = (uint)(value1?.GetHashCode() ?? 0),
                hc2 = (uint)(value2?.GetHashCode() ?? 0),
                hc3 = (uint)(value3?.GetHashCode() ?? 0),
                hc4 = (uint)(value4?.GetHashCode() ?? 0),
                hc5 = (uint)(value5?.GetHashCode() ?? 0),
                hc6 = (uint)(value6?.GetHashCode() ?? 0);

            Initialize(out var v1, out var v2, out var v3, out var v4);

            v1 = Round(v1, hc1);
            v2 = Round(v2, hc2);
            v3 = Round(v3, hc3);
            v4 = Round(v4, hc4);

            var hash = MixState(v1, v2, v3, v4) + 24;
            hash = QueueRound(hash, hc5);
            hash = QueueRound(hash, hc6);

            return (int)MixFinal(hash);
        }

        ///<summary> Combines seven values into a hash code. </summary>
        ///<typeparam name="T1"> The type of the first value to combine into the hash code. </typeparam>
        ///<typeparam name="T2"> The type of the second value to combine into the hash code. </typeparam>
        ///<typeparam name="T3"> The type of the third value to combine into the hash code. </typeparam>
        ///<typeparam name="T4"> The type of the fourth value to combine into the hash code. </typeparam>
        ///<typeparam name="T5"> The type of the fifth value to combine into the hash code. </typeparam>
        ///<typeparam name="T6"> The type of the sixth value to combine into the hash code. </typeparam>
        ///<typeparam name="T7"> The type of the seventh value to combine into the hash code. </typeparam>
        ///<param name="value1"> The first value to combine into the hash code. </param>
        ///<param name="value2"> The second value to combine into the hash code. </param>
        ///<param name="value3"> The third value to combine into the hash code. </param>
        ///<param name="value4"> The fourth value to combine into the hash code. </param>
        ///<param name="value5"> The fifth value to combine into the hash code. </param>
        ///<param name="value6"> The sixth value to combine into the hash code. </param>
        ///<param name="value7"> The seventh value to combine into the hash code. </param>
        ///<returns> The hash code that represents the seven values. </returns>
        public static int Combine<T1, T2, T3, T4, T5, T6, T7>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7)
        {
            uint hc1 = (uint)(value1?.GetHashCode() ?? 0),
                hc2 = (uint)(value2?.GetHashCode() ?? 0),
                hc3 = (uint)(value3?.GetHashCode() ?? 0),
                hc4 = (uint)(value4?.GetHashCode() ?? 0),
                hc5 = (uint)(value5?.GetHashCode() ?? 0),
                hc6 = (uint)(value6?.GetHashCode() ?? 0),
                hc7 = (uint)(value7?.GetHashCode() ?? 0);

            Initialize(out var v1, out var v2, out var v3, out var v4);

            v1 = Round(v1, hc1);
            v2 = Round(v2, hc2);
            v3 = Round(v3, hc3);
            v4 = Round(v4, hc4);

            var hash = MixState(v1, v2, v3, v4) + 28;
            hash = QueueRound(hash, hc5);
            hash = QueueRound(hash, hc6);
            hash = QueueRound(hash, hc7);

            return (int)MixFinal(hash);
        }

        ///<summary> Combines eight values into a hash code. </summary>
        ///<typeparam name="T1"> The type of the first value to combine into the hash code. </typeparam>
        ///<typeparam name="T2"> The type of the second value to combine into the hash code. </typeparam>
        ///<typeparam name="T3"> The type of the third value to combine into the hash code. </typeparam>
        ///<typeparam name="T4"> The type of the fourth value to combine into the hash code. </typeparam>
        ///<typeparam name="T5"> The type of the fifth value to combine into the hash code. </typeparam>
        ///<typeparam name="T6"> The type of the sixth value to combine into the hash code. </typeparam>
        ///<typeparam name="T7"> The type of the seventh value to combine into the hash code. </typeparam>
        ///<typeparam name="T8"> The type of the eighth value to combine into the hash code. </typeparam>
        ///<param name="value1"> The first value to combine into the hash code. </param>
        ///<param name="value2"> The second value to combine into the hash code. </param>
        ///<param name="value3"> The third value to combine into the hash code. </param>
        ///<param name="value4"> The fourth value to combine into the hash code. </param>
        ///<param name="value5"> The fifth value to combine into the hash code. </param>
        ///<param name="value6"> The sixth value to combine into the hash code. </param>
        ///<param name="value7"> The seventh value to combine into the hash code. </param>
        ///<param name="value8"> The eighth value to combine into the hash code. </param>
        ///<returns> The hash code that represents the eight values. </returns>
        public static int Combine<T1, T2, T3, T4, T5, T6, T7, T8>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8)
        {
            uint hc1 = (uint)(value1?.GetHashCode() ?? 0),
                hc2 = (uint)(value2?.GetHashCode() ?? 0),
                hc3 = (uint)(value3?.GetHashCode() ?? 0),
                hc4 = (uint)(value4?.GetHashCode() ?? 0),
                hc5 = (uint)(value5?.GetHashCode() ?? 0),
                hc6 = (uint)(value6?.GetHashCode() ?? 0),
                hc7 = (uint)(value7?.GetHashCode() ?? 0),
                hc8 = (uint)(value8?.GetHashCode() ?? 0);

            Initialize(out var v1, out var v2, out var v3, out var v4);

            v1 = Round(v1, hc1);
            v2 = Round(v2, hc2);
            v3 = Round(v3, hc3);
            v4 = Round(v4, hc4);

            v1 = Round(v1, hc5);
            v2 = Round(v2, hc6);
            v3 = Round(v3, hc7);
            v4 = Round(v4, hc8);

            var hash = MixState(v1, v2, v3, v4) + 32;
            return (int)MixFinal(hash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Initialize(out uint v1, out uint v2, out uint v3, out uint v4)
        {
            v1 = s_seed + prime1 + prime2;
            v2 = s_seed + prime2;
            v3 = s_seed;
            v4 = s_seed - prime1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint Round(uint hash, uint input) => RotateLeft(hash + input * prime2, 13) * prime1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint QueueRound(uint hash, uint queuedValue) => RotateLeft(hash + queuedValue * prime3, 17) * prime4;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint MixState(uint v1, uint v2, uint v3, uint v4) => RotateLeft(v1, 1) + RotateLeft(v2, 7) + RotateLeft(v3, 12) + RotateLeft(v4, 18);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint RotateLeft(uint value, int offset) => (value << offset) | (value >> (32 - offset));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint MixEmptyState() => s_seed + prime5;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint MixFinal(uint hash)
        {
            hash ^= hash >> 15;
            hash *= prime2;
            hash ^= hash >> 13;
            hash *= prime3;
            hash ^= hash >> 16;
            return hash;
        }

        ///<summary> Adds a single value to the hash combiner, optionally specifying the type that provides the hash code function. </summary>
        ///<typeparam name="T"> The type of the value to add to the hash code. </typeparam>
        ///<param name="value"> The value to add to the hash code. </param>
        ///<param name="comparer"> The <see cref="IEqualityComparer{T}"/> to use to calculate the hash code. This value can be <see langword="null"/>, which will use the default equality comparer for <typeparamref name="T"/>. </param>
        public void Add<T>(T value, IEqualityComparer<T> comparer = null) => Add(value.Equals(null) ? 0 : (comparer?.GetHashCode(value) ?? value.GetHashCode()));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Add(int hash)
        {
            var val = (uint)hash;

            var previousLength = ++_length;
            var position = previousLength & 3;

            if (position == 0) _queue1 = val;
            else if (position == 1) _queue2 = val;
            else if (position == 2) _queue3 = val;
            else
            {
                if (previousLength == 3) Initialize(out _v1, out _v2, out _v3, out _v4);

                _v1 = Round(_v1, _queue1);
                _v2 = Round(_v2, _queue2);
                _v3 = Round(_v3, _queue3);
                _v4 = Round(_v4, val);
            }
        }

        Exception ensureSingleton;

        ///<summary> Calculates the final hash code after consecutive <see cref="Add{T}"/> invocations. </summary>
        ///<remarks> This method can be called <b>only once</b> per <see cref="HashCode"/> instance. </remarks>
        ///<returns> The calculated hash code. </returns>
        ///<exception cref="InvalidOperationException"> This method was called more than once. </exception>
        public int ToHashCode()
        {
            if (ensureSingleton is null) ensureSingleton = new InvalidOperationException("Cannot call GetHashCode more than once per instance");
            else throw ensureSingleton;

            var length = _length;
            var position = length & 3;
            var hash = (length < 4 ? MixEmptyState() : MixState(_v1, _v2, _v3, _v4)) + length * 4;

            if (position > 0)
            {
                hash = QueueRound(hash, _queue1);
                if (position > 1)
                {
                    hash = QueueRound(hash, _queue2);
                    if (position > 2) hash = QueueRound(hash, _queue3);
                }
            }

            return (int)MixFinal(hash);
        }
    }
}
