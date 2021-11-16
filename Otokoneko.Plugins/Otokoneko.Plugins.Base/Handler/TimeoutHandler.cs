using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Otokoneko.Plugins.Base.Handler
{
    public class TimeoutHandler : DelegatingHandler
    {
        public TimeSpan Timeout { get; set; }

        public TimeoutHandler(HttpMessageHandler innerHandler, TimeSpan? timeout=null)
            : base(innerHandler)
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(100);
        }

        private async Task<HttpResponseMessage> Delay(
        CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout, cancellationToken);

            return null;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var delayTask = Delay(cancellationToken);
            var firstCompleted = await Task.WhenAny(
                base.SendAsync(request, cancellationToken), delayTask);

            if (firstCompleted != delayTask)
                return await firstCompleted;
            else
                throw new TimeoutException($"The request was canceled due to the configured TimeoutHandler.Timeout of {Timeout.TotalSeconds} seconds elapsing.");
        }

    }
}
