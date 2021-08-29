#if CLIENT
using Otokoneko.Base.Network;
using Otokoneko.Utils;
using SuperSocket.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using SuperSocket.Channel;
using Timer = System.Timers.Timer;

namespace Otokoneko.Client
{
    public class RequestPriority: IComparable<RequestPriority>
    {
        private readonly int _priority;
        private readonly long _objectId;

        public RequestPriority(int priority, long objectId)
        {
            _priority = priority;
            _objectId = objectId;
        }

        public int CompareTo(RequestPriority? other)
        {
            if (_priority < other._priority)
            {
                return -1;
            }
            else if (_priority == other._priority)
            {
                if (_objectId < other._objectId)
                {
                    return -1;
                }
                else if (_objectId == other._objectId)
                {
                    return 0;
                }

            }

            return 1;
        }
    }

    public class RequestWithTimeout
    {
        public Request Request { get; }
        private DateTime Timeout { get; }

        public bool IsTimeout => DateTime.Now >= Timeout;

        public RequestWithTimeout(Request request, int millisecondsTimeout)
        {
            Request = request;
            Timeout = DateTime.Now + TimeSpan.FromMilliseconds(millisecondsTimeout);
        }
    }

    public class Client
    {
        // private readonly Proxy _proxy;
        private int _requestId;
        private bool _running;
        private int _connecting;
        private const int MaxSizeOfUncheckedRequestList = 8;
        private readonly ServerConfig _serverConfig;
        private readonly List<IEasyClient<Response, Request>> _clients;
        private readonly AsyncQueue<RequestPriority, RequestWithTimeout> _toBeSendRequests;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<Response>> _processResponses;
        private readonly ConcurrentDictionary<EasyClient<Response>, ThreadSafeList<Tuple<DateTime, RequestWithTimeout>>> _uncheckedRequests;
        private readonly Func<Response, Task> _processServerNotify;

        public Client(ServerConfig serverConfig, Func<Response, Task> processServerNotify)
        {
            // _proxy = proxy;
            _processServerNotify = processServerNotify;
            _serverConfig = serverConfig ?? throw new ArgumentNullException(nameof(serverConfig));
            _connecting = 0;
            _toBeSendRequests = new AsyncQueue<RequestPriority, RequestWithTimeout>();
            _requestId = 0;
            _running = true;
            _processResponses = new ConcurrentDictionary<int, TaskCompletionSource<Response>>();
            _uncheckedRequests = new ConcurrentDictionary<EasyClient<Response>, ThreadSafeList<Tuple<DateTime, RequestWithTimeout>>>();
            var decoder = new FixedHeaderResponseDecoder()
            {
                Decoder = new ResponseDecoder()
            };
            _clients = new List<IEasyClient<Response, Request>>();
            for (var i = 0; i < _serverConfig.Hosts.Count; i++)
            {
                var client =
                    new EasyClient<Response, Request>(decoder, new MessageEncoder(),
                        new ChannelOptions() {MaxPackageLength = 64 * 1024 * 1024})
                    {
                        Security = new SecurityOptions()
                        {
                            EnabledSslProtocols = SslProtocols.None,
                            RemoteCertificateValidationCallback = (sender, certificate, chain, errors) => 
                                certificate?.GetCertHashString() == _serverConfig.CertificateHash
                        }
                    };
                client.PackageHandler += ResponseProcess;
                client.Closed += OnChannelClosed;
                _clients.Add(client);
                _uncheckedRequests.TryAdd(client, new ThreadSafeList<Tuple<DateTime, RequestWithTimeout>>());
                var index = i;
                Task.Run(async () => { await SendAllRequestsInQueue(client, index); });
            }

            var retryTimer = new Timer()
            {
                AutoReset = true,
                Enabled = true,
                Interval = 60 * 1000
            };
            retryTimer.Elapsed += RequestRetry;
        }

        private void RequestRetry(object obj, ElapsedEventArgs args)
        {
            foreach (var (_, requests) in _uncheckedRequests)
            {
                foreach (var tuple in requests.ToList().Where(it => it != null))
                {
                    var (time, request) = tuple;
                    // 发出的请求3分钟内未收到回复且未超时，则重新发送请求
                    if ((DateTime.Now - time).TotalMinutes > 3 && !request.IsTimeout)
                    {
                        requests.Remove(tuple);
                        _toBeSendRequests.Enqueue(request, new RequestPriority(0, request.Request.Id)).AsTask().Wait();
                    }
                }
            }
        }

        // 当连接关闭时
        private void OnChannelClosed(object sender, EventArgs args)
        {
            var client = (EasyClient<Response>)sender;
            if(!_uncheckedRequests.TryGetValue(client, out var uncheckedRequest)) return;
            var requests = uncheckedRequest.GetAndClearAll();
            // 立即重试
            foreach (var (_, request) in requests)
            {
                _toBeSendRequests.Enqueue(request, new RequestPriority(0, request.Request.Id)).AsTask().Wait();
            }
        }

        private async ValueTask SendAllRequestsInQueue(IEasyClient<Response, Request> client, int index)
        {
            if(!_uncheckedRequests.TryGetValue((EasyClient<Response>)client, out var uncheckedRequest)) return;
            var success = false;
            while (!success)
            {
                success = await Connect(index);
                if (!success) Thread.Sleep(1000);
            }
            while (_running)
            {
                if (_uncheckedRequests.Count > MaxSizeOfUncheckedRequestList)
                {
                    Thread.Sleep(100);
                    continue;
                }
                var request = await _toBeSendRequests.Dequeue();
                // 若请求已超时，则放弃
                if(request.IsTimeout) continue;
                try
                {
                    await client.SendAsync(request.Request);
                    uncheckedRequest.Add(new Tuple<DateTime, RequestWithTimeout>(DateTime.Now, request));
                }
                catch
                {
                    if (!_running) return;
                    await _toBeSendRequests.Enqueue(request,
                        new RequestPriority(0, request.Request.Id));
                    success = await Connect(index);
                    if (!success) Thread.Sleep(200);
                }
            }
        }

        private async ValueTask ResponseProcess(EasyClient<Response> client, Response response)
        {
            Trace.WriteLine($"response: {response.Id}");
            if(!_uncheckedRequests.TryGetValue(client, out var uncheckedRequest)) return;
            if (uncheckedRequest.Any(it => it.Item2.Request.Id == response.Id))
            {
                uncheckedRequest.Remove(uncheckedRequest.Single(it => it.Item2.Request.Id == response.Id));
            }
            if (_processResponses.ContainsKey(response.Id))
            {
                _processResponses.TryRemove(response.Id, out var taskCompletionSource);
                taskCompletionSource?.TrySetResult(response);
            }
            else if (response.Id == -1)
            {
                // 使用 await 会发生死锁
                _processServerNotify(response);
            }
        }

        public async Task<bool> Connect(int index)
        {
            if (Interlocked.Increment(ref _connecting) != 1)
            {
                Interlocked.Decrement(ref _connecting);
                return false;
            }

            try
            {
                var hostAndPort = _serverConfig.Hosts[index];
                var task = _clients[index]
                    .ConnectAsync(new IPEndPoint(IPAddress.Parse(hostAndPort.Host), hostAndPort.Port));
                var success = await task;
                if (success)
                {
                    _clients[index].StartReceive();
                }

                return success;
            }
            finally
            {
                Interlocked.Decrement(ref _connecting);
            }
        }

        public void Close()
        {
            _running = false;
        }

        public async Task Send(Request request, TaskCompletionSource<Response> taskCompletionSource, int millisecondsTimeout, int priority = 1)
        {
            request.Id = Interlocked.Increment(ref _requestId);
            Trace.WriteLine($"request: {request.Id}");
            _processResponses.TryAdd(request.Id, taskCompletionSource);
            await _toBeSendRequests.Enqueue(new RequestWithTimeout(request, millisecondsTimeout),
                new RequestPriority(priority, request.Id));
        }
    }
}
#endif