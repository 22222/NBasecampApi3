using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NBasecampApi3.Launchpad
{
    /// <summary>
    /// Handles creating <see cref="IHttpClient"/> instances, deciding when and 
    /// if an internal <see cref="HttpClient"/> instance should be shared.
    /// </summary>
    internal class HttpClientPool : IDisposable
    {
        /// <summary>
        /// A default instance of <see cref="HttpClientPool"/>.
        /// </summary>
        public static readonly HttpClientPool Default = new NoDisposeHttpClientSource();
        private class NoDisposeHttpClientSource : HttpClientPool { public override void Dispose() { } }

        private ConcurrentDictionary<HttpClientHandlerOptions, HttpClientNode> cachedHttpClientDictionary = new ConcurrentDictionary<HttpClientHandlerOptions, HttpClientNode>();

        /// <summary>
        /// Creates and returns a <see cref="IHttpClient"/>, 
        /// which may share an internal <see cref="HttpClient"/> with other instances.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="HttpClient"/>, this is meant to be a short lived instance for one use.  
        /// You can and should dispose this instance when you're done with it. 
        /// </remarks>
        /// <returns>the client</returns>
        public IHttpClient Create()
        {
            return Create(handlerOptions: null);
        }

        /// <summary>
        /// Creates and returns a <see cref="IHttpClient"/> using the given options, 
        /// which may share an internal <see cref="HttpClient"/> with other instances.
        /// </summary>
        /// <param name="handlerOptions"></param>
        /// <returns></returns>
        public IHttpClient Create(HttpClientHandlerOptions handlerOptions)
        {
            // You're not supposed to create an HttpClient for each request:
            // * https://github.com/mspnp/performance-optimization/blob/master/ImproperInstantiation/docs/ImproperInstantiation.md
            // * https://aspnetmonsters.com/2016/08/2016-08-27-httpclientwrong/

            // But an HttpClient maybe never updates its DNS cache, so you 
            // don't necessarily want to keep one open forever:
            // * https://github.com/dotnet/corefx/issues/11224
            // * http://byterot.blogspot.co.uk/2016/07/singleton-httpclient-dns.html
            // * http://www.nimaara.com/2016/11/01/beware-of-the-net-httpclient/

            // So for now we're going to keep a shared HttpClient alive, but 
            // only for an hour or so before we recreate it.

            if (handlerOptions == null)
            {
                handlerOptions = CreateDefaultHttpClientHandlerOptions();
            }

            HttpClientNode httpClientNode;
            HttpClientNode oldHttpClientNode = null;
            if (cachedHttpClientDictionary.TryGetValue(handlerOptions, out httpClientNode))
            {
                if (httpClientNode.ExpirationTimeUtc < DateTime.UtcNow)
                {
                    cachedHttpClientDictionary.TryRemove(handlerOptions, out oldHttpClientNode);
                    httpClientNode = null;
                }
            }

            if (httpClientNode == null)
            {
                httpClientNode = cachedHttpClientDictionary.GetOrAdd(handlerOptions, CreateHttpClientNode);
            }

            var result = new HttpClientAdapter(httpClientNode.HttpClient);
            return result;
        }

        /// <summary>
        /// Creates and returns the default HttpClient 
        /// </summary>
        /// <returns></returns>
        protected virtual HttpClientHandlerOptions CreateDefaultHttpClientHandlerOptions()
        {
            return new HttpClientHandlerOptions();
        }

        protected virtual HttpMessageHandler CreateHttpMessageHandler(HttpClientHandlerOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var handler = new HttpClientHandler();
            handler.AllowAutoRedirect = options.AllowAutoRedirect;
            handler.AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate;
            handler.UseCookies = false;
            return handler;
        }

        private HttpClientNode CreateHttpClientNode(HttpClientHandlerOptions handlerOptions)
        {
            var httpClientHandler = CreateHttpMessageHandler(handlerOptions);
            var httpClient = new HttpClient(httpClientHandler);
            var expirationTimeUtc = DateTime.UtcNow.AddHours(1);
            return new HttpClientNode(httpClient, expirationTimeUtc);
        }

        /// <summary>
        /// Disposes this source and all <see cref="IHttpClient"/> 
        /// instances that have been created by it.
        /// </summary>
        public virtual void Dispose()
        {
            foreach (var kv in cachedHttpClientDictionary)
            {
                kv.Value.HttpClient.Dispose();
            }
            cachedHttpClientDictionary.Clear();
        }

        private sealed class HttpClientNode
        {
            public HttpClientNode(HttpClient httpClient, DateTime expirationTimeUtc)
            {
                if (httpClient == null) throw new ArgumentException(nameof(httpClient));
                this.HttpClient = httpClient;
                this.ExpirationTimeUtc = expirationTimeUtc;
            }

            public HttpClient HttpClient { get; }
            public DateTime ExpirationTimeUtc { get; }
        }

        private sealed class HttpClientAdapter : IHttpClient
        {
            private HttpClient httpClient;

            public HttpClientAdapter(HttpClient httpClient)
            {
                if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));
                this.httpClient = httpClient;
            }

            private HttpClient HttpClient
            {
                get
                {
                    if (httpClient == null) throw new ObjectDisposedException(nameof(HttpClientAdapter));
                    return httpClient;
                }
            }

            Task<HttpResponseMessage> IHttpClient.SendAsync(HttpRequestMessage request) => HttpClient.SendAsync(request);
            Task<HttpResponseMessage> IHttpClient.SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => HttpClient.SendAsync(request, cancellationToken);
            Task<HttpResponseMessage> IHttpClient.SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption) => HttpClient.SendAsync(request, completionOption);
            Task<HttpResponseMessage> IHttpClient.SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken) => HttpClient.SendAsync(request, completionOption, cancellationToken);
            void IDisposable.Dispose()
            {
                // Not disposing this because the whole point is to share this with other instances.
                httpClient = null;
            }
        }
    }

    /// <summary>
    /// A subset of the options for <see cref="HttpClientHandler"/> 
    /// in an immutable class that implements <see cref="IEquatable{T}"/>.
    /// </summary>
    internal sealed class HttpClientHandlerOptions : IEquatable<HttpClientHandlerOptions>
    {
        public HttpClientHandlerOptions(bool allowAutoRedirect = true)
        {
            AllowAutoRedirect = allowAutoRedirect;
        }

        /// <summary>
        /// See <see cref="HttpClientHandler.AllowAutoRedirect"/>.
        /// </summary>
        public bool AllowAutoRedirect { get; }

        /// <summary>
        /// Calls <see cref="Equals(HttpClientHandlerOptions)"/>
        /// </summary>
        public override bool Equals(object obj)
        {
            return Equals(obj as HttpClientHandlerOptions);
        }

        /// <inheritdoc />
        public bool Equals(HttpClientHandlerOptions other)
        {
            if (object.ReferenceEquals(this, other)) return true;
            if (object.ReferenceEquals(other, null)) return false;

            return AllowAutoRedirect == other.AllowAutoRedirect;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + AllowAutoRedirect.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>
    /// An subset of <see cref="HttpClient"/> that only exposes some non-mutation methods.
    /// </summary>
    internal interface IHttpClient : IDisposable
    {
        /// <summary>
        /// Send an HTTP request as an asynchronous operation.
        /// </summary>
        Task<HttpResponseMessage> SendAsync(HttpRequestMessage request);

        /// <summary>
        /// Send an HTTP request as an asynchronous operation.
        /// </summary>
        Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption);

        /// <summary>
        ///  Send an HTTP request as an asynchronous operation.
        /// </summary>
        Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken);

        /// <summary>
        /// Send an HTTP request as an asynchronous operation.
        /// </summary>
        Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken);
    }
}