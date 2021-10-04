using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Otokoneko.Server.Utils
{
    public static class ServerConfigStringGenerator
    {
        public static string ServerConfigStringGenerate(string host, int port, string certificateHash, string serverName, string serverId)
        {
            var config = new
            {
                Hosts = new List<object>()
                {
                    new
                    {
                        Host = host,
                        Port = port
                    }
                },
                CertificateHash = certificateHash,
                ServerId = serverId,
                ServerName = serverName
            };
            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(config));
            return Convert.ToBase64String(bytes);
        }
    }
}
