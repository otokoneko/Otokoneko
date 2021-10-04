using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Otokoneko.Server.Utils;

namespace Otokoneko.Server.Config
{
    public class CertificateConfig
    {
        [Description("证书存放地址，如该位置不存在证书文件，将使用提供的密码生成一个证书")]
        public string Path { get; set; } = "./certificate/server.pfx";
        public string Password { get; set; } = "Otokoneko";
    }

    public class ServerConfig
    {
        public string Host { get; set; } = "127.0.0.1";
        public ushort Port { get; set; } = 23333;
    }

    public class LogConfig
    {
        [Description("日志路径")] 
        public string Path { get; set; } = "./log/";

        [Description("日志级别：All, Debug, Info, Warn, Error, Fatal, Off")] 
        public string Level { get; set; } = "All";

        [Description("最多保留的日志个数，-1为全部保留")] 
        public int MaxSizeRollBackups { get; set; } = 5;
    }

    public class Config
    {
        [Description("便于记忆的服务器名称")]
        public string Name { get; set; } = "Otokoneko";

        [Description("区分服务器的唯一Id")]
        public string Id { get; set; } = EncryptUtils.GenerateRandomString(16);

        [Description("Tls协议使用的证书")]
        public CertificateConfig Certificate { get; set; } = new CertificateConfig();

        [Description("本地监听端口")]
        public ushort Port { get; set; } = 23333;

        [Description("访问服务器的地址与端口，用于生成供客户端使用的配置字符串")]
        public List<ServerConfig> Servers { get; set; } =
            Dns.GetHostAddresses(Dns.GetHostName())
                .Where(it => it.AddressFamily == AddressFamily.InterNetwork)
                .Select(it => new ServerConfig()
                {
                    Host = it.ToString(),
                    Port = 23333
                }).ToList();

        public LogConfig LogConfig { get; set; } = new LogConfig();
    }
}
