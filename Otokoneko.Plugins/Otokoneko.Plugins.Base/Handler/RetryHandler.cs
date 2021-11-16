using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Otokoneko.Plugins.Base.Handler
{
    public class RetryHandler : DelegatingHandler
    {
        public int MaxRetries { get; set; }

        private int _minRetryDelayMs;
        public int MinRetryDelayMs
        {
            get => _minRetryDelayMs;
            set
            {
                if (value <= MaxRetryDelayMs) _minRetryDelayMs = value;
            }
        }

        private int _maxRetryDelayMs;
        public int MaxRetryDelayMs
        {
            get => _maxRetryDelayMs;
            set
            {
                if (value >= MinRetryDelayMs) _maxRetryDelayMs = value;
            }
        }

        public RetryHandler(HttpMessageHandler innerHandler, int maxRetries=3, int minRetryDelayMs=1000, int maxRetryDelayMs=10000)
            : base(innerHandler)
        {
            MaxRetries = maxRetries;
            MinRetryDelayMs = minRetryDelayMs;
            MaxRetryDelayMs = maxRetryDelayMs;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            for (var i = 0; i < MaxRetries; i++)
            {
                var response = await base.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                await Task.Delay(new Random().Next(MinRetryDelayMs, MaxRetryDelayMs), cancellationToken);
            }

            return null;
        }
    }
}
