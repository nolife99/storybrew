using System;
using System.IO;
namespace BrewLib.Util.LZMA.Compress.LzmaAlone
{
    using BrewLib.Util.LZMA;
    using BrewLib.Util.LZMA.Common;
    using BrewLib.Util.LZMA.Compress.LZMA;
    using System.Globalization;

    public class CDoubleStream : Stream
    {
        public Stream s1, s2;
        public int fileIndex;
        public long skipSize;

        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override bool CanSeek => false;
        public override long Length => s1.Length + s2.Length - skipSize;
        public override long Position
        {
            get => 0;
            set => throw new NotImplementedException();
        }

        public override void Flush() => throw new NotImplementedException();
        public override int Read(byte[] buffer, int offset, int count)
        {
            var numTotal = 0;
            while (count > 0)
            {
                if (fileIndex == 0)
                {
                    var num = s1.Read(buffer, offset, count);
                    offset += num;
                    count -= num;
                    numTotal += num;
                    if (num == 0) fileIndex++;
                }
                if (fileIndex == 1)
                {
                    numTotal += s2.Read(buffer, offset, count);
                    return numTotal;
                }
            }
            return numTotal;
        }
        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
        public override void SetLength(long value) => throw new NotImplementedException();
    }

    class LzmaAlone
    {
        enum Key
        {
            Help1 = 0,
            Help2,
            Mode,
            Dictionary,
            FastBytes,
            LitContext,
            LitPos,
            PosBits,
            MatchFinder,
            EOS,
            StdIn,
            StdOut,
            Train
        };

        static void PrintHelp()
        {
            Console.WriteLine("\nUsage:  LZMA <e|d> [<switches>...] inputFile outputFile\n" +
                "  e: encode file\n" +
                "  d: decode file\n" +
                "  b: Benchmark\n" +
                "<Switches>\n" +
                // "  -a{N}:  set compression mode - [0, 1], default: 1 (max)\n" +
                "  -d{N}:  set dictionary - [0, 29], default: 23 (8MB)\n" +
                "  -fb{N}: set number of fast bytes - [5, 273], default: 128\n" +
                "  -lc{N}: set number of literal context bits - [0, 8], default: 3\n" +
                "  -lp{N}: set number of literal pos bits - [0, 4], default: 0\n" +
                "  -pb{N}: set number of pos bits - [0, 4], default: 2\n" +
                "  -mf{MF_ID}: set Match Finder: [bt2, bt4], default: bt4\n" +
                "  -eos:   write End Of Stream marker\n"
            // + "  -si:    read data from stdin\n"
            // + "  -so:    write data to stdout\n"
            );
        }

        static bool GetNumber(string s, out int v)
        {
            v = 0;
            for (var i = 0; i < s.Length; ++i)
            {
                var c = s[i];
                if (c < '0' || c > '9') return false;
                v *= 10;
                v += c - '0';
            }
            return true;
        }

        static int IncorrectCommand() => throw new InvalidDataException("Command line error");

        static int Main2(string[] args)
        {
            Console.WriteLine("\nLZMA# 4.61  2008-11-23\n");

            if (args.Length == 0)
            {
                PrintHelp();
                return 0;
            }

            SwitchForm[] kSwitchForms =
            {
                new SwitchForm("?", SwitchType.Simple, false),
                new SwitchForm("H", SwitchType.Simple, false),
                new SwitchForm("A", SwitchType.UnLimitedPostString, false, 1),
                new SwitchForm("D", SwitchType.UnLimitedPostString, false, 1),
                new SwitchForm("FB", SwitchType.UnLimitedPostString, false, 1),
                new SwitchForm("LC", SwitchType.UnLimitedPostString, false, 1),
                new SwitchForm("LP", SwitchType.UnLimitedPostString, false, 1),
                new SwitchForm("PB", SwitchType.UnLimitedPostString, false, 1),
                new SwitchForm("MF", SwitchType.UnLimitedPostString, false, 1),
                new SwitchForm("EOS", SwitchType.Simple, false),
                new SwitchForm("SI", SwitchType.Simple, false),
                new SwitchForm("SO", SwitchType.Simple, false),
                new SwitchForm("T", SwitchType.UnLimitedPostString, false, 1)
            };

            Parser parser = new(kSwitchForms.Length);
            try
            {
                parser.ParseStrings(kSwitchForms, args);
            }
            catch
            {
                return IncorrectCommand();
            }

            if (parser[(int)Key.Help1].ThereIs || parser[(int)Key.Help2].ThereIs)
            {
                PrintHelp();
                return 0;
            }

            var nonSwitchStrings = parser.NonSwitchStrings;

            var paramIndex = 0;
            if (paramIndex >= nonSwitchStrings.Count) return IncorrectCommand();
            var command = nonSwitchStrings[paramIndex++];
            command = command.ToLower(CultureInfo.InvariantCulture);

            var dictionaryIsDefined = false;
            var dictionary = 1 << 21;
            if (parser[(int)Key.Dictionary].ThereIs)
            {
                if (!GetNumber(parser[(int)Key.Dictionary].PostStrings[0], out int dicLog)) IncorrectCommand();
                dictionary = 1 << dicLog;
                dictionaryIsDefined = true;
            }

            var mf = "bt4";
            if (parser[(int)Key.MatchFinder].ThereIs) mf = parser[(int)Key.MatchFinder].PostStrings[0];
            mf = mf.ToLower(CultureInfo.InvariantCulture);

            if (command == "b")
            {
                const int kNumDefaultItereations = 10;
                var numIterations = kNumDefaultItereations;
                if (paramIndex < nonSwitchStrings.Count) if (!GetNumber(nonSwitchStrings[paramIndex++], out numIterations)) numIterations = kNumDefaultItereations;
                return LzmaBench.LzmaBenchmark(numIterations, (uint)dictionary);
            }

            var train = string.Empty;
            if (parser[(int)Key.Train].ThereIs) train = parser[(int)Key.Train].PostStrings[0];

            var encodeMode = false;
            if (command == "e") encodeMode = true;
            else if (command == "d") encodeMode = false;
            else IncorrectCommand();

            var stdInMode = parser[(int)Key.StdIn].ThereIs;
            var stdOutMode = parser[(int)Key.StdOut].ThereIs;

            Stream inStream;
            if (stdInMode) throw new NotImplementedException();
            else
            {
                if (paramIndex >= nonSwitchStrings.Count) IncorrectCommand();
                var inputName = nonSwitchStrings[paramIndex++];
                inStream = new FileStream(inputName, FileMode.Open, FileAccess.Read);
            }

            Stream outStream;
            if (stdOutMode) throw new NotImplementedException();
            else
            {
                if (paramIndex >= nonSwitchStrings.Count) IncorrectCommand();
                var outputName = nonSwitchStrings[paramIndex++];
                outStream = new FileStream(outputName, FileMode.Create, FileAccess.Write);
            }

            FileStream trainStream = null;
            if (train.Length != 0) trainStream = new FileStream(train, FileMode.Open, FileAccess.Read);

            if (encodeMode)
            {
                if (!dictionaryIsDefined) dictionary = 1 << 23;

                var posStateBits = 2;
                var litContextBits = 3;
                var litPosBits = 0;
                var algorithm = 2;
                var numFastBytes = 128;

                var eos = parser[(int)Key.EOS].ThereIs || stdInMode;

                if (parser[(int)Key.Mode].ThereIs) if (!GetNumber(parser[(int)Key.Mode].PostStrings[0], out algorithm)) IncorrectCommand();
                if (parser[(int)Key.FastBytes].ThereIs) if (!GetNumber(parser[(int)Key.FastBytes].PostStrings[0], out numFastBytes)) IncorrectCommand();
                if (parser[(int)Key.LitContext].ThereIs) if (!GetNumber(parser[(int)Key.LitContext].PostStrings[0], out litContextBits)) IncorrectCommand();
                if (parser[(int)Key.LitPos].ThereIs) if (!GetNumber(parser[(int)Key.LitPos].PostStrings[0], out litPosBits)) IncorrectCommand();
                if (parser[(int)Key.PosBits].ThereIs) if (!GetNumber(parser[(int)Key.PosBits].PostStrings[0], out posStateBits)) IncorrectCommand();

                CoderPropID[] propIDs =
                {
                    CoderPropID.DictionarySize,
                    CoderPropID.PosStateBits,
                    CoderPropID.LitContextBits,
                    CoderPropID.LitPosBits,
                    CoderPropID.Algorithm,
                    CoderPropID.NumFastBytes,
                    CoderPropID.MatchFinder,
                    CoderPropID.EndMarker
                };
                object[] properties =
                {
                    dictionary,
                    posStateBits,
                    litContextBits,
                    litPosBits,
                    algorithm,
                    numFastBytes,
                    mf,
                    eos
                };

                var encoder = new Encoder();
                encoder.SetCoderProperties(propIDs, properties);
                encoder.WriteCoderProperties(outStream);

                long fileSize;
                if (eos || stdInMode) fileSize = -1;
                else fileSize = inStream.Length;
                for (var i = 0; i < 8; ++i) outStream.WriteByte((byte)(fileSize >> 8 * i));

                if (trainStream != null)
                {
                    var doubleStream = new CDoubleStream
                    {
                        s1 = trainStream,
                        s2 = inStream,
                        fileIndex = 0
                    };
                    inStream = doubleStream;

                    var trainFileSize = trainStream.Length;
                    doubleStream.skipSize = 0;
                    if (trainFileSize > dictionary) doubleStream.skipSize = trainFileSize - dictionary;
                    trainStream.Seek(doubleStream.skipSize, SeekOrigin.Begin);
                    encoder.SetTrainSize((uint)(trainFileSize - doubleStream.skipSize));
                }
                encoder.Code(inStream, outStream, -1, -1, null);
            }
            else if (command == "d")
            {
                var properties = new byte[5];
                if (inStream.Read(properties, 0, 5) != 5) throw new ArgumentOutOfRangeException("input .lzma is too short");
                Decoder decoder = new();
                decoder.SetDecoderProperties(properties);

                if (trainStream != null) if (!decoder.Train(trainStream)) throw new NotSupportedException("can't train");
                var outSize = 0L;
                for (var i = 0; i < 8; ++i)
                {
                    var v = inStream.ReadByte();
                    if (v < 0) throw new IndexOutOfRangeException("Can't Read 1");
                    outSize |= (long)(byte)v << 8 * i;
                }
                long compressedSize = inStream.Length - inStream.Position;
                decoder.Code(inStream, outStream, compressedSize, outSize, null);
            }
            else throw new ApplicationException("Command Error");
            return 0;
        }

        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                return Main2(args);
            }
            catch (Exception e)
            {
                Console.WriteLine("{0} Caught exception #1.", e);
                return 1;
            }
        }
    }
}