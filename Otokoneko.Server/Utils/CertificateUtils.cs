using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Otokoneko.Server.Utils
{
    public static class CertificateUtils
    {
        public static X509Certificate2 GetCertificate(string path, string password)
        {
            if (!File.Exists(path))
            {
                GenerateCertificate(path, password);
            }
            return new X509Certificate2(path, password);
        }

        public static void GenerateCertificate(string path, string password)
        {
            var req = new CertificateRequest("cn=Otokoneko", RSA.Create(4096), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var cert = req.CreateSelfSigned(DateTimeOffset.Now.AddDays(-10), DateTimeOffset.Now.AddYears(10));
            File.WriteAllBytes(path, cert.Export(X509ContentType.Pfx, password));
        }
    }
}
