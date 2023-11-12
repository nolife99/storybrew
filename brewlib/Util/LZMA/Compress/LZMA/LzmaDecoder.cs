using System;

namespace BrewLib.Util.LZMA.Compress.LZMA
{
    using BrewLib.Util.LZMA;
    using BrewLib.Util.LZMA.Compress.LZ;
    using RangeCoder;
    using System.IO;

    public class Decoder : ICoder, ISetDecoderProperties
    {
        class LenDecoder
        {
            BitDecoder m_Choice, m_Choice2;
            readonly BitTreeDecoder[] m_LowCoder = new BitTreeDecoder[Base.kNumPosStatesMax], m_MidCoder = new BitTreeDecoder[Base.kNumPosStatesMax];
            readonly BitTreeDecoder m_HighCoder = new(Base.kNumHighLenBits);
            uint m_NumPosStates;

            public void Create(uint numPosStates)
            {
                for (var posState = m_NumPosStates; posState < numPosStates; ++posState)
                {
                    m_LowCoder[posState] = new BitTreeDecoder(Base.kNumLowLenBits);
                    m_MidCoder[posState] = new BitTreeDecoder(Base.kNumMidLenBits);
                }
                m_NumPosStates = numPosStates;
            }

            public void Init()
            {
                m_Choice.Init();
                for (var posState = 0u; posState < m_NumPosStates; ++posState)
                {
                    m_LowCoder[posState].Init();
                    m_MidCoder[posState].Init();
                }
                m_Choice2.Init();
                m_HighCoder.Init();
            }

            public uint Decode(RangeCoder.Decoder rangeDecoder, uint posState)
            {
                if (m_Choice.Decode(rangeDecoder) == 0) return m_LowCoder[posState].Decode(rangeDecoder);
                else
                {
                    var symbol = Base.kNumLowLenSymbols;
                    if (m_Choice2.Decode(rangeDecoder) == 0) symbol += m_MidCoder[posState].Decode(rangeDecoder);
                    else
                    {
                        symbol += Base.kNumMidLenSymbols;
                        symbol += m_HighCoder.Decode(rangeDecoder);
                    }
                    return symbol;
                }
            }
        }

        class LiteralDecoder
        {
            struct Decoder2
            {
                BitDecoder[] m_Decoders;

                public void Create() => m_Decoders = new BitDecoder[0x300];
                public readonly void Init()
                {
                    for (var i = 0; i < 768; ++i) m_Decoders[i].Init();
                }

                public readonly byte DecodeNormal(RangeCoder.Decoder rangeDecoder)
                {
                    var symbol = 1u;
                    do symbol = symbol << 1 | m_Decoders[symbol].Decode(rangeDecoder);
                    while (symbol < 256);
                    return (byte)symbol;
                }

                public readonly byte DecodeWithMatchByte(RangeCoder.Decoder rangeDecoder, byte matchByte)
                {
                    uint symbol = 1;
                    do
                    {
                        var matchBit = (uint)(matchByte >> 7) & 1;
                        matchByte <<= 1;

                        var bit = m_Decoders[(1 + matchBit << 8) + symbol].Decode(rangeDecoder);
                        symbol = symbol << 1 | bit;

                        if (matchBit != bit)
                        {
                            while (symbol < 256) symbol = symbol << 1 | m_Decoders[symbol].Decode(rangeDecoder);
                            break;
                        }
                    }
                    while (symbol < 256);
                    return (byte)symbol;
                }
            }

            Decoder2[] m_Coders;
            int m_NumPrevBits, m_NumPosBits;
            uint m_PosMask;

            public void Create(int numPosBits, int numPrevBits)
            {
                if (m_Coders != null && m_NumPrevBits == numPrevBits && m_NumPosBits == numPosBits) return;
                m_NumPosBits = numPosBits;
                m_PosMask = ((uint)1 << numPosBits) - 1;
                m_NumPrevBits = numPrevBits;

                var numStates = (uint)1 << m_NumPrevBits + m_NumPosBits;
                m_Coders = new Decoder2[numStates];
                for (var i = 0u; i < numStates; ++i) m_Coders[i].Create();
            }

            public void Init()
            {
                var numStates = (uint)1 << m_NumPrevBits + m_NumPosBits;
                for (var i = 0u; i < numStates; ++i) m_Coders[i].Init();
            }

            uint GetState(uint pos, byte prevByte) => ((pos & m_PosMask) << m_NumPrevBits) + (uint)(prevByte >> 8 - m_NumPrevBits);

            public byte DecodeNormal(RangeCoder.Decoder rangeDecoder, uint pos, byte prevByte) => m_Coders[GetState(pos, prevByte)].DecodeNormal(rangeDecoder);
            public byte DecodeWithMatchByte(RangeCoder.Decoder rangeDecoder, uint pos, byte prevByte, byte matchByte) => m_Coders[GetState(pos, prevByte)].DecodeWithMatchByte(rangeDecoder, matchByte);
        };

        readonly OutWindow m_OutWindow = new();
        readonly RangeCoder.Decoder m_RangeDecoder = new();

        readonly BitDecoder[] m_IsMatchDecoders = new BitDecoder[Base.kNumStates << Base.kNumPosStatesBitsMax],
            m_IsRepDecoders = new BitDecoder[Base.kNumStates],
            m_IsRepG0Decoders = new BitDecoder[Base.kNumStates],
            m_IsRepG1Decoders = new BitDecoder[Base.kNumStates],
            m_IsRepG2Decoders = new BitDecoder[Base.kNumStates],
            m_IsRep0LongDecoders = new BitDecoder[Base.kNumStates << Base.kNumPosStatesBitsMax],
            m_PosDecoders = new BitDecoder[Base.kNumFullDistances - Base.kEndPosModelIndex];

        readonly BitTreeDecoder[] m_PosSlotDecoder = new BitTreeDecoder[Base.kNumLenToPosStates];
        readonly BitTreeDecoder m_PosAlignDecoder = new(Base.kNumAlignBits);

        readonly LenDecoder m_LenDecoder = new(), m_RepLenDecoder = new();

        readonly LiteralDecoder m_LiteralDecoder = new();

        uint m_DictionarySize, m_DictionarySizeCheck, m_PosStateMask;

        public Decoder()
        {
            m_DictionarySize = 0xFFFFFFFF;
            for (var i = 0; i < Base.kNumLenToPosStates; ++i) m_PosSlotDecoder[i] = new BitTreeDecoder(Base.kNumPosSlotBits);
        }

        void SetDictionarySize(uint dictionarySize)
        {
            if (m_DictionarySize != dictionarySize)
            {
                m_DictionarySize = dictionarySize;
                m_DictionarySizeCheck = Math.Max(m_DictionarySize, 1);
                var blockSize = Math.Max(m_DictionarySizeCheck, 1 << 12);
                m_OutWindow.Create(blockSize);
            }
        }

        void SetLiteralProperties(int lp, int lc)
        {
            if (lp > 8) throw new ArgumentException(nameof(lp));
            if (lc > 8) throw new ArgumentException(nameof(lc));
            m_LiteralDecoder.Create(lp, lc);
        }

        void SetPosBitsProperties(int pb)
        {
            if (pb > Base.kNumPosStatesBitsMax) throw new ArgumentException(nameof(pb));
            var numPosStates = (uint)1 << pb;
            m_LenDecoder.Create(numPosStates);
            m_RepLenDecoder.Create(numPosStates);
            m_PosStateMask = numPosStates - 1;
        }

        bool _solid;
        void Init(Stream inStream, Stream outStream)
        {
            m_RangeDecoder.Init(inStream);
            m_OutWindow.Init(outStream, _solid);

            uint i;
            for (i = 0; i < Base.kNumStates; ++i)
            {
                for (uint j = 0; j <= m_PosStateMask; ++j)
                {
                    var index = (i << Base.kNumPosStatesBitsMax) + j;
                    m_IsMatchDecoders[index].Init();
                    m_IsRep0LongDecoders[index].Init();
                }
                m_IsRepDecoders[i].Init();
                m_IsRepG0Decoders[i].Init();
                m_IsRepG1Decoders[i].Init();
                m_IsRepG2Decoders[i].Init();
            }

            m_LiteralDecoder.Init();
            for (i = 0; i < Base.kNumLenToPosStates; ++i) m_PosSlotDecoder[i].Init();
            for (i = 0; i < Base.kNumFullDistances - Base.kEndPosModelIndex; ++i) m_PosDecoders[i].Init();

            m_LenDecoder.Init();
            m_RepLenDecoder.Init();
            m_PosAlignDecoder.Init();
        }

        public void Code(Stream inStream, Stream outStream, long inSize, long outSize, ICodeProgress progress)
        {
            Init(inStream, outStream);

            var state = new Base.State();
            state.Init();
            uint rep0 = 0, rep1 = 0, rep2 = 0, rep3 = 0;

            var nowPos64 = 0ul;
            var outSize64 = (ulong)outSize;
            if (nowPos64 < outSize64)
            {
                if (m_IsMatchDecoders[state.Index << Base.kNumPosStatesBitsMax].Decode(m_RangeDecoder) != 0) throw new InvalidDataException();
                state.UpdateChar();
                var b = m_LiteralDecoder.DecodeNormal(m_RangeDecoder, 0, 0);
                m_OutWindow.PutByte(b);
                ++nowPos64;
            }
            while (nowPos64 < outSize64)
            {
                var posState = (uint)nowPos64 & m_PosStateMask;
                if (m_IsMatchDecoders[(state.Index << Base.kNumPosStatesBitsMax) + posState].Decode(m_RangeDecoder) == 0)
                {
                    byte b;
                    var prevByte = m_OutWindow.GetByte(0);
                    if (!state.IsCharState()) b = m_LiteralDecoder.DecodeWithMatchByte(m_RangeDecoder, (uint)nowPos64, prevByte, m_OutWindow.GetByte(rep0));
                    else b = m_LiteralDecoder.DecodeNormal(m_RangeDecoder, (uint)nowPos64, prevByte);
                    m_OutWindow.PutByte(b);
                    state.UpdateChar();
                    ++nowPos64;
                }
                else
                {
                    uint len;
                    if (m_IsRepDecoders[state.Index].Decode(m_RangeDecoder) == 1)
                    {
                        if (m_IsRepG0Decoders[state.Index].Decode(m_RangeDecoder) == 0 && m_IsRep0LongDecoders[(state.Index << Base.kNumPosStatesBitsMax) + posState].Decode(m_RangeDecoder) == 0)
                        {
                            state.UpdateShortRep();
                            m_OutWindow.PutByte(m_OutWindow.GetByte(rep0));
                            ++nowPos64;
                            continue;
                        }
                        else
                        {
                            uint distance;
                            if (m_IsRepG1Decoders[state.Index].Decode(m_RangeDecoder) == 0) distance = rep1;
                            else
                            {
                                if (m_IsRepG2Decoders[state.Index].Decode(m_RangeDecoder) == 0) distance = rep2;
                                else
                                {
                                    distance = rep3;
                                    rep3 = rep2;
                                }
                                rep2 = rep1;
                            }
                            rep1 = rep0;
                            rep0 = distance;
                        }
                        len = m_RepLenDecoder.Decode(m_RangeDecoder, posState) + Base.kMatchMinLen;
                        state.UpdateRep();
                    }
                    else
                    {
                        rep3 = rep2;
                        rep2 = rep1;
                        rep1 = rep0;

                        len = Base.kMatchMinLen + m_LenDecoder.Decode(m_RangeDecoder, posState);
                        state.UpdateMatch();

                        var posSlot = m_PosSlotDecoder[Base.GetLenToPosState(len)].Decode(m_RangeDecoder);
                        if (posSlot >= Base.kStartPosModelIndex)
                        {
                            var numDirectBits = (int)((posSlot >> 1) - 1);
                            rep0 = (2 | posSlot & 1) << numDirectBits;
                            if (posSlot < Base.kEndPosModelIndex) rep0 += BitTreeDecoder.ReverseDecode(m_PosDecoders, rep0 - posSlot - 1, m_RangeDecoder, numDirectBits);
                            else
                            {
                                rep0 += m_RangeDecoder.DecodeDirectBits(numDirectBits - Base.kNumAlignBits) << Base.kNumAlignBits;
                                rep0 += m_PosAlignDecoder.ReverseDecode(m_RangeDecoder);
                            }
                        }
                        else rep0 = posSlot;
                    }
                    if (rep0 >= m_OutWindow.TrainSize + nowPos64 || rep0 >= m_DictionarySizeCheck)
                    {
                        if (rep0 == 0xFFFFFFFF) break;
                        throw new InvalidDataException();
                    }
                    m_OutWindow.CopyBlock(rep0, len);
                    nowPos64 += len;
                }
            }
            m_OutWindow.Flush();
            m_OutWindow.ReleaseStream();
            m_RangeDecoder.ReleaseStream();
        }

        public void SetDecoderProperties(byte[] properties)
        {
            if (properties.Length < 5) throw new ArgumentException(nameof(properties));
            var lc = properties[0] % 9;
            var remainder = properties[0] / 9;

            var lp = remainder % 5;
            var pb = remainder / 5;
            if (pb > Base.kNumPosStatesBitsMax) throw new ArgumentException(nameof(pb));
            var dictionarySize = 0u;
            for (var i = 0; i < 4; ++i) dictionarySize += (uint)properties[1 + i] << i * 8;

            SetDictionarySize(dictionarySize);
            SetLiteralProperties(lp, lc);
            SetPosBitsProperties(pb);
        }

        public bool Train(Stream stream)
        {
            _solid = true;
            return m_OutWindow.Train(stream);
        }
    }
}