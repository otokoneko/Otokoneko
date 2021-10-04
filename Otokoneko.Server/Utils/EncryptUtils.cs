using System.Security.Cryptography;
using System.Text;

namespace Otokoneko.Server.Utils
{
    public static class EncryptUtils
    {
        public static byte[] GenerateRandomBytes(int numberOfBytes)
        {
            using var cryptoProvider = new RNGCryptoServiceProvider();
            var bytes = new byte[numberOfBytes];
            cryptoProvider.GetBytes(bytes);
            return bytes;
        }

        public static string GenerateRandomString(int numberOfBytes)
        {
            using var cryptoProvider = new RNGCryptoServiceProvider();
            var bytes = new byte[numberOfBytes];
            cryptoProvider.GetBytes(bytes);
            var stringBuilder = new StringBuilder();
            foreach (var b in bytes)
            {
                stringBuilder.Append(b.ToString("X2"));
            }
            return stringBuilder.ToString();
        }

        public static bool ComparePassword(string a, string b)
        {
            var res = a.Length ^ b.Length;
            for (var i = 0; i < a.Length && i < b.Length; i++)
            {
                res |= a[i] ^ b[i];
            }
            return res == 0;
        }
    }
}
