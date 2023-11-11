// LzmaBench.cs

using BrewLib.Util.LZMA;
using BrewLib.Util.LZMA.Common;
using BrewLib.Util.LZMA.Compress.LZMA;
using System;
using System.IO;

namespace BrewLib.Util.LZMA.Compress.LzmaAlone
{
    /// <summary>
    /// LZMA Benchmark
    /// </summary>
    internal abstract class LzmaBench
    {
        const uint kAdditionalSize = 6 << 20;
        const uint kCompressedAdditionalSize = 1 << 10;
        const uint kMaxLzmaPropSize = 10;

        class CRandomGenerator
        {
            uint A1;
            uint A2;
            public CRandomGenerator() { Init(); }
            public void Init() { A1 = 362436069; A2 = 521288629; }
            public uint GetRnd()
            {
                return
                    (A1 = 36969 * (A1 & 0xffff) + (A1 >> 16)) << 16 ^
                    (A2 = 18000 * (A2 & 0xffff) + (A2 >> 16));
            }
        };

        class CBitRandomGenerator
        {
            readonly CRandomGenerator RG = new();
            uint Value;
            int NumBits;
            public void Init()
            {
                Value = 0;
                NumBits = 0;
            }
            public uint GetRnd(int numBits)
            {
                uint result;
                if (NumBits > numBits)
                {
                    result = Value & ((uint)1 << numBits) - 1;
                    Value >>= numBits;
                    NumBits -= numBits;
                    return result;
                }
                numBits -= NumBits;
                result = Value << numBits;
                Value = RG.GetRnd();
                result |= Value & ((uint)1 << numBits) - 1;
                Value >>= numBits;
                NumBits = 32 - numBits;
                return result;
            }
        };

        class CBenchRandomGenerator
        {
            readonly CBitRandomGenerator RG = new();
            uint Pos;
            uint Rep0;

            public uint BufferSize;
            public byte[] Buffer;

            public CBenchRandomGenerator() { }

            public void Set(uint bufferSize)
            {
                Buffer = new byte[bufferSize];
                Pos = 0;
                BufferSize = bufferSize;
            }
            uint GetRndBit() { return RG.GetRnd(1); }
            uint GetLogRandBits(int numBits)
            {
                uint len = RG.GetRnd(numBits);
                return RG.GetRnd((int)len);
            }
            uint GetOffset()
            {
                if (GetRndBit() == 0)
                    return GetLogRandBits(4);
                return GetLogRandBits(4) << 10 | RG.GetRnd(10);
            }
            uint GetLen1() { return RG.GetRnd(1 + (int)RG.GetRnd(2)); }
            uint GetLen2() { return RG.GetRnd(2 + (int)RG.GetRnd(2)); }
            public void Generate()
            {
                RG.Init();
                Rep0 = 1;
                while (Pos < BufferSize)
                {
                    if (GetRndBit() == 0 || Pos < 1)
                        Buffer[Pos++] = (byte)RG.GetRnd(8);
                    else
                    {
                        uint len;
                        if (RG.GetRnd(3) == 0)
                            len = 1 + GetLen1();
                        else
                        {
                            do
                                Rep0 = GetOffset();
                            while (Rep0 >= Pos);
                            Rep0++;
                            len = 2 + GetLen2();
                        }
                        for (uint i = 0; i < len && Pos < BufferSize; i++, Pos++)
                            Buffer[Pos] = Buffer[Pos - Rep0];
                    }
                }
            }
        };

        class CrcOutStream : Stream
        {
            public CRC CRC = new();
            public void Init() { CRC.Init(); }
            public uint GetDigest() { return CRC.GetDigest(); }

            public override bool CanRead { get { return false; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return true; } }
            public override long Length { get { return 0; } }
            public override long Position { get { return 0; } set { } }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) { return 0; }
            public override void SetLength(long value) { }
            public override int Read(byte[] buffer, int offset, int count) { return 0; }

            public override void WriteByte(byte b)
            {
                CRC.UpdateByte(b);
            }
            public override void Write(byte[] buffer, int offset, int count)
            {
                CRC.Update(buffer, (uint)offset, (uint)count);
            }
        };

        class CProgressInfo : ICodeProgress
        {
            public long ApprovedStart;
            public long InSize;
            public DateTime Time;
            public void Init() { InSize = 0; }
            public void SetProgress(long inSize, long outSize)
            {
                if (inSize >= ApprovedStart && InSize == 0)
                {
                    Time = DateTime.UtcNow;
                    InSize = inSize;
                }
            }
        }
        const int kSubBits = 8;

        static uint GetLogSize(uint size)
        {
            for (int i = kSubBits; i < 32; i++)
                for (uint j = 0; j < 1 << kSubBits; j++)
                    if (size <= ((uint)1 << i) + (j << i - kSubBits))
                        return (uint)(i << kSubBits) + j;
            return 32 << kSubBits;
        }

        static ulong MyMultDiv64(ulong value, ulong elapsedTime)
        {
            ulong freq = TimeSpan.TicksPerSecond;
            ulong elTime = elapsedTime;
            while (freq > 1000000)
            {
                freq >>= 1;
                elTime >>= 1;
            }
            if (elTime == 0)
                elTime = 1;
            return value * freq / elTime;
        }

        static ulong GetCompressRating(uint dictionarySize, ulong elapsedTime, ulong size)
        {
            ulong t = GetLogSize(dictionarySize) - (18 << kSubBits);
            ulong numCommandsForOne = 1060 + (t * t * 10 >> 2 * kSubBits);
            ulong numCommands = size * numCommandsForOne;
            return MyMultDiv64(numCommands, elapsedTime);
        }

        static ulong GetDecompressRating(ulong elapsedTime, ulong outSize, ulong inSize)
        {
            ulong numCommands = inSize * 220 + outSize * 20;
            return MyMultDiv64(numCommands, elapsedTime);
        }

        static ulong GetTotalRating(
            uint dictionarySize,
            ulong elapsedTimeEn, ulong sizeEn,
            ulong elapsedTimeDe,
            ulong inSizeDe, ulong outSizeDe)
        {
            return (GetCompressRating(dictionarySize, elapsedTimeEn, sizeEn) +
                GetDecompressRating(elapsedTimeDe, inSizeDe, outSizeDe)) / 2;
        }

        static void PrintValue(ulong v)
        {
            string s = v.ToString();
            for (int i = 0; i + s.Length < 6; i++)
                Console.Write(" ");
            Console.Write(s);
        }

        static void PrintRating(ulong rating)
        {
            PrintValue(rating / 1000000);
            Console.Write(" MIPS");
        }

        static void PrintResults(
            uint dictionarySize,
            ulong elapsedTime,
            ulong size,
            bool decompressMode, ulong secondSize)
        {
            ulong speed = MyMultDiv64(size, elapsedTime);
            PrintValue(speed / 1024);
            Console.Write(" KB/s  ");
            ulong rating;
            if (decompressMode)
                rating = GetDecompressRating(elapsedTime, size, secondSize);
            else
                rating = GetCompressRating(dictionarySize, elapsedTime, size);
            PrintRating(rating);
        }

        static public int LzmaBenchmark(int numIterations, uint dictionarySize)
        {
            if (numIterations <= 0)
                return 0;
            if (dictionarySize < 1 << 18)
            {
                Console.WriteLine("\nError: dictionary size for benchmark must be >= 19 (512 KB)");
                return 1;
            }
            Console.Write("\n       Compressing                Decompressing\n\n");

            Encoder encoder = new();
            Decoder decoder = new();

            CoderPropID[] propIDs =
            {
                CoderPropID.DictionarySize,
            };
            object[] properties =
            {
                (int)dictionarySize,
            };

            uint kBufferSize = dictionarySize + kAdditionalSize;
            uint kCompressedBufferSize = kBufferSize / 2 + kCompressedAdditionalSize;

            encoder.SetCoderProperties(propIDs, properties);
            MemoryStream propStream = new();
            encoder.WriteCoderProperties(propStream);
            byte[] propArray = propStream.ToArray();

            CBenchRandomGenerator rg = new();

            rg.Set(kBufferSize);
            rg.Generate();
            CRC crc = new();
            crc.Init();
            crc.Update(rg.Buffer, 0, rg.BufferSize);

            CProgressInfo progressInfo = new()
            {
                ApprovedStart = dictionarySize
            };

            ulong totalBenchSize = 0;
            ulong totalEncodeTime = 0;
            ulong totalDecodeTime = 0;
            ulong totalCompressedSize = 0;

            MemoryStream inStream = new(rg.Buffer, 0, (int)rg.BufferSize);
            MemoryStream compressedStream = new((int)kCompressedBufferSize);
            CrcOutStream crcOutStream = new();
            for (int i = 0; i < numIterations; i++)
            {
                progressInfo.Init();
                inStream.Seek(0, SeekOrigin.Begin);
                compressedStream.Seek(0, SeekOrigin.Begin);
                encoder.Code(inStream, compressedStream, -1, -1, progressInfo);
                TimeSpan sp2 = DateTime.UtcNow - progressInfo.Time;
                ulong encodeTime = (ulong)sp2.Ticks;

                long compressedSize = compressedStream.Position;
                if (progressInfo.InSize == 0)
                    throw new Exception("Internal ERROR 1282");

                ulong decodeTime = 0;
                for (int j = 0; j < 2; j++)
                {
                    compressedStream.Seek(0, SeekOrigin.Begin);
                    crcOutStream.Init();

                    decoder.SetDecoderProperties(propArray);
                    ulong outSize = kBufferSize;
                    DateTime startTime = DateTime.UtcNow;
                    decoder.Code(compressedStream, crcOutStream, 0, (long)outSize, null);
                    TimeSpan sp = DateTime.UtcNow - startTime;
                    decodeTime = (ulong)sp.Ticks;
                    if (crcOutStream.GetDigest() != crc.GetDigest())
                        throw new Exception("CRC Error");
                }
                ulong benchSize = kBufferSize - (ulong)progressInfo.InSize;
                PrintResults(dictionarySize, encodeTime, benchSize, false, 0);
                Console.Write("     ");
                PrintResults(dictionarySize, decodeTime, kBufferSize, true, (ulong)compressedSize);
                Console.WriteLine();

                totalBenchSize += benchSize;
                totalEncodeTime += encodeTime;
                totalDecodeTime += decodeTime;
                totalCompressedSize += (ulong)compressedSize;
            }
            Console.WriteLine("---------------------------------------------------");
            PrintResults(dictionarySize, totalEncodeTime, totalBenchSize, false, 0);
            Console.Write("     ");
            PrintResults(dictionarySize, totalDecodeTime,
                    kBufferSize * (ulong)numIterations, true, totalCompressedSize);
            Console.WriteLine("    Average");
            return 0;
        }
    }
}
