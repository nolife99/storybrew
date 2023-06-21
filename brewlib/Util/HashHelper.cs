using System.Globalization;
using System.IO;
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

            var characters = new char[data.Length * 2];
            for (var i = 0; i < data.Length; ++i) data[i].ToString("x2", CultureInfo.InvariantCulture.NumberFormat).CopyTo(0, characters, i * 2, 2);

            return new string(characters);
        }

        public static string GetFileMd5(string path) => GetMd5(GetFileMd5Bytes(path));
        public static byte[] GetFileMd5Bytes(string path)
        {
            using (var md5 = MD5.Create()) using (var stream = File.OpenRead(path)) return md5.ComputeHash(stream);
        }

        ///<summary> Obfuscates a <see cref="string"/>; or deobfuscates a <see cref="string"/> that was obfuscated by this method. </summary>
        ///<param name="input"> The <see cref="string"/> to obfuscate, or an obfuscated string. </param>
        ///<returns> The obfuscated <see cref="string"/>; or if <paramref name="input"/> was obfuscated, the deobfuscated <see cref="string"/>. </returns>
        public static string EncodeDecodeString(this string input)
        {
            var result = new char[input.Length];
            for (var i = 0; i < input.Length; ++i) result[i] = (char)(input[i] ^ input.Length);
            return new string(result);
        }

        ///<summary> Encodes a <see cref="string"/> and writes it to a <see cref="BinaryWriter"/>. </summary>
        ///<inheritdoc cref="BinaryWriter.Write(string)"/>
        public static void WriteEncodedString(this BinaryWriter writer, string input) => writer.Write(EncodeDecodeString(input));

        ///<summary> Reads an encoded <see cref="string"/> from a <see cref="BinaryReader"/> and returns the decoded <see cref="string"/>. </summary>
        ///<returns> The decoded <see cref="string"/> that was read. </returns>
        ///<inheritdoc cref="BinaryReader.ReadString"/>
        public static string ReadEncodedString(this BinaryReader reader) => EncodeDecodeString(reader.ReadString());
    }
}