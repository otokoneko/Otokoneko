using System.Security.Cryptography;
using System.Text;

namespace Otokoneko.Utils
{
    public static class PasswordHashProvider
    {
        private const int HashByteSize = 64;

        public static string CreateHash(string username, string password, int iterations = 1 << 15)
        {
            using var sha512 = SHA512.Create();
            var salt = sha512.ComputeHash(Encoding.UTF8.GetBytes(username));
            var hash = Pbkdf2(password, salt, iterations, HashByteSize);
            var sb = new StringBuilder();
            foreach (var _ in hash)
            {
                sb.Append(_.ToString("X2"));
            }
            return sb.ToString();
        }

        public static string CreateHash(byte[] salt, string password, int iterations = 1 << 15)
        {
            using var sha512 = SHA512.Create();
            var hash = Pbkdf2(password, salt, iterations, HashByteSize);
            var sb = new StringBuilder();
            foreach (var _ in hash)
            {
                sb.Append(_.ToString("X2"));
            }
            return sb.ToString();
        }

        private static byte[] Pbkdf2(string password, byte[] salt, int iterations, int outputBytes)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt) { IterationCount = iterations };
            return pbkdf2.GetBytes(outputBytes);
        }
    }

}
