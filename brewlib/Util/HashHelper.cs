using System.Collections.Generic;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace BrewLib.Util
{
    public static class HashHelper
    {
        public static string GetMd5(string value) => GetMd5(Encoding.ASCII.GetBytes(value));
        public static string GetMd5(byte[] data)
        {
            using (var md5 = MD5.Create()) data = md5.ComputeHash(data);

            var chars = new StringBuilder(data.Length * 2);
            for (var i = 0; i < data.Length; ++i) chars.Append(data[i].ToString("x2", CultureInfo.InvariantCulture));

            return chars.ToString();
        }

        public static string GetFileMd5(string path)
        {
            var data = GetFileMd5Bytes(path);

            var chars = new StringBuilder(data.Length * 2);
            for (var i = 0; i < data.Length; ++i) chars.Append(data[i].ToString("x2", CultureInfo.InvariantCulture));

            return chars.ToString();
        }
        public static byte[] GetFileMd5Bytes(string path)
        {
            using (var md5 = MD5.Create()) using (var stream = File.OpenRead(path)) return md5.ComputeHash(stream);
        }
    }

    public struct HashCode
    {
        static readonly uint s_seed = BitConverter.ToUInt32(new FastRandom().NextBytes(sizeof(uint)), 0);

        const uint Prime1 = 0x9E3779B1, Prime2 = 0x85EBCA77, Prime3 = 0xC2B2AE3D, Prime4 = 0x27D4EB2F, Prime5 = 0x165667B1;
        uint _v1, _v2, _v3, _v4, _queue1, _queue2, _queue3, _length;

        public HashCode(params int[] hashes) : this()
        {
            for (var i = 0; i < hashes.Length; ++i) Add(hashes[i]);
        }

        public static int Combine<T1>(T1 value1)
        {
            uint hc1 = (uint)(value1?.GetHashCode() ?? 0), hash = MixEmptyState() + 4;
            hash = QueueRound(hash, hc1);

            return (int)MixFinal(hash);
        }
        public static int Combine<T1, T2>(T1 value1, T2 value2)
        {
            uint hc1 = (uint)(value1?.GetHashCode() ?? 0), hc2 = (uint)(value2?.GetHashCode() ?? 0), hash = MixEmptyState() + 8;
            hash = QueueRound(hash, hc1);
            hash = QueueRound(hash, hc2);

            return (int)MixFinal(hash);
        }
        public static int Combine<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
        {
            uint hc1 = (uint)(value1?.GetHashCode() ?? 0), hc2 = (uint)(value2?.GetHashCode() ?? 0), hc3 = (uint)(value3?.GetHashCode() ?? 0), hash = MixEmptyState() + 12;
            hash = QueueRound(hash, hc1);
            hash = QueueRound(hash, hc2);
            hash = QueueRound(hash, hc3);

            return (int)MixFinal(hash);
        }
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
            v1 = s_seed + Prime1 + Prime2;
            v2 = s_seed + Prime2;
            v3 = s_seed;
            v4 = s_seed - Prime1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint Round(uint hash, uint input) => RotateLeft(hash + input * Prime2, 13) * Prime1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint QueueRound(uint hash, uint queuedValue) => RotateLeft(hash + queuedValue * Prime3, 17) * Prime4;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint MixState(uint v1, uint v2, uint v3, uint v4) => RotateLeft(v1, 1) + RotateLeft(v2, 7) + RotateLeft(v3, 12) + RotateLeft(v4, 18);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint RotateLeft(uint value, int offset) => (value << offset) | (value >> (32 - offset));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint MixEmptyState() => s_seed + Prime5;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint MixFinal(uint hash)
        {
            hash ^= hash >> 15;
            hash *= Prime2;
            hash ^= hash >> 13;
            hash *= Prime3;
            hash ^= hash >> 16;
            return hash;
        }

        public void Add<T>(T value) => Add(value?.GetHashCode() ?? 0);
        public void Add<T>(T value, IEqualityComparer<T> comparer) => Add(value.Equals(null) ? 0 : (comparer?.GetHashCode(value) ?? value.GetHashCode()));

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

        public override int GetHashCode()
        {
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