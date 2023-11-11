using System;

namespace BrewLib.Util.LZMA.Compress.RangeCoder
{
    struct BitEncoder
    {
        public const int kNumBitModelTotalBits = 11, kNumBitPriceShiftBits = 6;
        public const uint kBitModelTotal = 1 << kNumBitModelTotalBits;
        const int kNumMoveBits = 5, kNumMoveReducingBits = 2;
        uint Prob;

        public void Init() => Prob = kBitModelTotal >> 1;

        public void UpdateModel(uint symbol)
        {
            if (symbol == 0) Prob += kBitModelTotal - Prob >> kNumMoveBits;
            else Prob -= Prob >> kNumMoveBits;
        }

        public void Encode(Encoder encoder, uint symbol)
        {
            var newBound = (encoder.Range >> kNumBitModelTotalBits) * Prob;
            if (symbol == 0)
            {
                encoder.Range = newBound;
                Prob += kBitModelTotal - Prob >> kNumMoveBits;
            }
            else
            {
                encoder.Low += newBound;
                encoder.Range -= newBound;
                Prob -= Prob >> kNumMoveBits;
            }
            if (encoder.Range < Encoder.kTopValue)
            {
                encoder.Range <<= 8;
                encoder.ShiftLow();
            }
        }

        static readonly uint[] ProbPrices = new uint[kBitModelTotal >> kNumMoveReducingBits];

        static BitEncoder()
        {
            const int kNumBits = kNumBitModelTotalBits - kNumMoveReducingBits;
            for (var i = kNumBits - 1; i >= 0; --i)
            {
                var start = 1u << kNumBits - i - 1;
                var end = 1u << kNumBits - i;
                for (var j = start; j < end; ++j) ProbPrices[j] = ((uint)i << kNumBitPriceShiftBits) + (end - j << kNumBitPriceShiftBits >> kNumBits - i - 1);
            }
        }

        public uint GetPrice(uint symbol) => ProbPrices[((Prob - symbol ^ -(int)symbol) & kBitModelTotal - 1) >> kNumMoveReducingBits];
        public uint GetPrice0() => ProbPrices[Prob >> kNumMoveReducingBits];
        public uint GetPrice1() => ProbPrices[kBitModelTotal - Prob >> kNumMoveReducingBits];
    }
    struct BitDecoder
    {
        public const int kNumBitModelTotalBits = 11;
        public const uint kBitModelTotal = 1 << kNumBitModelTotalBits;
        const int kNumMoveBits = 5;
        uint Prob;

        public void UpdateModel(int numMoveBits, uint symbol)
        {
            if (symbol == 0) Prob += kBitModelTotal - Prob >> numMoveBits;
            else Prob -= Prob >> numMoveBits;
        }

        public void Init() => Prob = kBitModelTotal >> 1;

        public uint Decode(Decoder rangeDecoder)
        {
            var newBound = (rangeDecoder.Range >> kNumBitModelTotalBits) * Prob;
            if (rangeDecoder.Code < newBound)
            {
                rangeDecoder.Range = newBound;
                Prob += kBitModelTotal - Prob >> kNumMoveBits;
                if (rangeDecoder.Range < Decoder.kTopValue)
                {
                    rangeDecoder.Code = rangeDecoder.Code << 8 | (byte)rangeDecoder.Stream.ReadByte();
                    rangeDecoder.Range <<= 8;
                }
                return 0;
            }
            else
            {
                rangeDecoder.Range -= newBound;
                rangeDecoder.Code -= newBound;
                Prob -= Prob >> kNumMoveBits;

                if (rangeDecoder.Range < Decoder.kTopValue)
                {
                    rangeDecoder.Code = rangeDecoder.Code << 8 | (byte)rangeDecoder.Stream.ReadByte();
                    rangeDecoder.Range <<= 8;
                }
                return 1;
            }
        }
    }
}