namespace SevenZip.Compression.RangeCoder
{
    readonly struct BitTreeEncoder
	{
        readonly BitEncoder[] Models;
        readonly int NumBitLevels;

		public BitTreeEncoder(int numBitLevels)
		{
			NumBitLevels = numBitLevels;
			Models = new BitEncoder[1 << numBitLevels];
		}

		public void Init()
		{
			for (var i = 1u; i < (1 << NumBitLevels); ++i) Models[i].Init();
		}

		public void Encode(Encoder rangeEncoder, uint symbol)
		{
            var m = 1u;
			for (var bitIndex = NumBitLevels; bitIndex > 0;)
			{
				--bitIndex;
                var bit = (symbol >> bitIndex) & 1;
				Models[m].Encode(rangeEncoder, bit);
				m = (m << 1) | bit;
			}
		}

		public void ReverseEncode(Encoder rangeEncoder, uint symbol)
		{
            var m = 1u;
			for (var i = 0; i < NumBitLevels; ++i)
			{
                uint bit = symbol & 1;
				Models[m].Encode(rangeEncoder, bit);
				m = (m << 1) | bit;
				symbol >>= 1;
			}
		}

		public uint GetPrice(uint symbol)
		{
            var price = 0u;
            var m = 1u;

			for (var bitIndex = NumBitLevels; bitIndex > 0;)
			{
				--bitIndex;
                var bit = (symbol >> bitIndex) & 1;
				price += Models[m].GetPrice(bit);
				m = (m << 1) + bit;
			}
			return price;
		}

		public uint ReverseGetPrice(uint symbol)
		{
            var price = 0u;
            var m = 1u;

			for (var i = NumBitLevels; i > 0; --i)
			{
                var bit = symbol & 1;
				symbol >>= 1;
				price += Models[m].GetPrice(bit);
				m = (m << 1) | bit;
			}
			return price;
		}

		public static uint ReverseGetPrice(BitEncoder[] Models, uint startIndex, int NumBitLevels, uint symbol)
		{
            var price = 0u;
            var m = 1u;

			for (var i = NumBitLevels; i > 0; --i)
			{
                var bit = symbol & 1;
				symbol >>= 1;
				price += Models[startIndex + m].GetPrice(bit);
				m = (m << 1) | bit;
			}
			return price;
		}

		public static void ReverseEncode(BitEncoder[] Models, uint startIndex, Encoder rangeEncoder, int NumBitLevels, uint symbol)
		{
            var m = 1u;
			for (var i = 0; i < NumBitLevels; ++i)
			{
                var bit = symbol & 1;
				Models[startIndex + m].Encode(rangeEncoder, bit);
				m = (m << 1) | bit;
				symbol >>= 1;
			}
		}
	}

    readonly struct BitTreeDecoder
	{
        readonly BitDecoder[] Models;
        readonly int NumBitLevels;

		public BitTreeDecoder(int numBitLevels)
		{
			NumBitLevels = numBitLevels;
			Models = new BitDecoder[1 << numBitLevels];
		}

		public void Init()
		{
			for (var i = 1u; i < (1 << NumBitLevels); ++i) Models[i].Init();
		}

		public uint Decode(Decoder rangeDecoder)
		{
            var m = 1u;
			for (var bitIndex = NumBitLevels; bitIndex > 0; --bitIndex) m = (m << 1) + Models[m].Decode(rangeDecoder);
			return m - ((uint)1 << NumBitLevels);
		}

		public uint ReverseDecode(Decoder rangeDecoder)
		{
            var m = 1u;
            var symbol = 1u;
			for (var bitIndex = 0; bitIndex < NumBitLevels; ++bitIndex)
			{
                var bit = Models[m].Decode(rangeDecoder);
				m <<= 1;
				m += bit;
				symbol |= bit << bitIndex;
			}
			return symbol;
		}

		public static uint ReverseDecode(BitDecoder[] Models, uint startIndex, Decoder rangeDecoder, int NumBitLevels)
		{
            var m = 1u;
            var symbol = 0u;
			for (var bitIndex = 0; bitIndex < NumBitLevels; ++bitIndex)
			{
				uint bit = Models[startIndex + m].Decode(rangeDecoder);
				m <<= 1;
				m += bit;
				symbol |= bit << bitIndex;
			}
			return symbol;
		}
	}
}