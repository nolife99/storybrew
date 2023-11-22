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
}