#if SERVER
using Microsoft.Extensions.Hosting;
using Otokoneko.Base.Network;
using SuperSocket;
using System;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Otokoneko.Server
{
    public partial class Server
    {
        private IHost _host;
        private readonly MessageEncoder _responseEncoder = new MessageEncoder();

        private void CreateHost(ushort port, X509Certificate certificate)
        {
            _host =
                SuperSocketHostBuilder.Create<Request, FixedHeaderRequestDecoder>()
                    .UsePackageHandler(async (session, request) =>
                    {
                        try
                        {
                            var result = await ProcessRequest(request, session);
                            switch (result)
                            {
                                case Response response:
                                    await session.SendAsync(_responseEncoder, response);
                                    break;
                                case Responses responses:
                                {
                                    await foreach (var response in responses)
                                    {
                                        await session.SendAsync(_responseEncoder, response);
                                    }

                                    break;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Warn(e);
                        }
                    })
                    .ConfigureSuperSocket((option) =>
                    {
                        option.Name = "Otokoneko Manga Server";
                        option.Listeners = new List<ListenOptions>()
                        {
                            new ListenOptions()
                            {
                                Security = SslProtocols.None,
                                Ip = "Any",
                                Port = port,
                                BackLog = 100,
                                CertificateOptions = new CertificateOptions()
                                {
                                    Certificate = certificate
                                }
                            }
                        };
                        option.MaxPackageLength = 64 * 1024 * 1024;
                    })
                    .ConfigureLogging((hostCtx, loggingBuilder) =>
                    {
                        loggingBuilder.ClearProviders();
                    })
                    .Build();
        }

        public async ValueTask Run()
        {
            Logger.Info($"开始监听端口: {Port}");
            await _host.RunAsync();
        }
    }
}
#endif