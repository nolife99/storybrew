namespace SevenZip.Compression.LZMA
{
	internal abstract class Base
	{
		public struct State
		{
			public uint Index;

			public void Init() => Index = 0;
			public void UpdateChar()
			{
				if (Index < 4) Index = 0;
				else if (Index < 10) Index -= 3;
				else Index -= 6;
			}

			public void UpdateMatch() => Index = (uint)(Index < 7 ? 7 : 10);
			public void UpdateRep() => Index = (uint)(Index < 7 ? 8 : 11);
			public void UpdateShortRep() => Index = (uint)(Index < 7 ? 9 : 11);
			public bool IsCharState() => Index < 7;
		}

		public const int kNumPosSlotBits = 6, kDicLogSizeMin = 0, kNumLenToPosStatesBits = 2;
		public const uint kNumLenToPosStates = 1 << kNumLenToPosStatesBits, kMatchMinLen = 2, kNumRepDistances = 4, kNumStates = 12;

		public static uint GetLenToPosState(uint len)
		{
			len -= kMatchMinLen;
			if (len < kNumLenToPosStates) return len;
			return kNumLenToPosStates - 1;
		}

		public const int kNumAlignBits = 4;
		public const uint kAlignTableSize = 1 << kNumAlignBits, kAlignMask = kAlignTableSize - 1;

		public const uint kStartPosModelIndex = 4, kEndPosModelIndex = 14, kNumPosModels = kEndPosModelIndex - kStartPosModelIndex;

		public const uint kNumFullDistances = 1 << ((int)kEndPosModelIndex / 2);

		public const uint kNumLitPosStatesBitsEncodingMax = 4, kNumLitContextBitsMax = 8;

		public const int kNumPosStatesBitsMax = 4;
		public const uint kNumPosStatesMax = 1 << kNumPosStatesBitsMax;
		public const int kNumPosStatesBitsEncodingMax = 4;
		public const uint kNumPosStatesEncodingMax = 1 << kNumPosStatesBitsEncodingMax;

		public const int kNumLowLenBits = 3, kNumMidLenBits = 3, kNumHighLenBits = 8;
		public const uint kNumLowLenSymbols = 1 << kNumLowLenBits, 
			kNumMidLenSymbols = 1 << kNumMidLenBits, 
			kNumLenSymbols = kNumLowLenSymbols + kNumMidLenSymbols + (1 << kNumHighLenBits),
			kMatchMaxLen = kMatchMinLen + kNumLenSymbols - 1;
	}
}