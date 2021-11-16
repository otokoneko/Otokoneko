#if ENABLE_RATELIMITER

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Bert.RateLimiters;

namespace Otokoneko.Plugins.Base.Handler
{
    public class RateLimitHandler : DelegatingHandler
    {
        public RateLimitHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        { }

        private FixedTokenBucket RateLimiter { get; set; }

        private int _requestIntervalMS;
        public int RequestIntervalMS
        {
            get => _requestIntervalMS;
            set
            {
                if (_requestIntervalMS == value) return;
                _requestIntervalMS = Math.Max(0, value);
                RateLimiter = _requestIntervalMS == 0 ? null : new FixedTokenBucket(1, 1, _requestIntervalMS);
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            while (RateLimiter != null && RateLimiter.ShouldThrottle(1, out var delayTime)) await Task.Delay(delayTime);
            return await base.SendAsync(request, cancellationToken);
        }
    }
}

#endif