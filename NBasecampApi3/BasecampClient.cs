using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Threading;

namespace NBasecampApi3
{
    /// <summary>
    /// A client for connecting to the basecamp API.
    /// </summary>
    public sealed class BasecampClient
    {
        static BasecampClient()
        {
            System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
        }

        private readonly Uri apiUri;
        private readonly IAccessTokenSource accessTokenSource;
        private readonly IRateLimiter rateLimiter;
        private readonly IResponseMessageCache responseMessageCache;
        private readonly string userAgent;
        private readonly HttpClientPool httpClientPool;

        /// <summary>
        /// Constructs a client for the specified account id, OAuth options, and refresh token.
        /// </summary>
        /// <param name="accountId">your basecamp account id</param>
        /// <param name="accessToken">an access token to use for authentication</param>
        public BasecampClient(int accountId, string accessToken)
            : this(accountId, new SimpleAccessTokenSource(accessToken)) { }

        /// <summary>
        /// Constructs a client for the specified account id and access token source.
        /// </summary>
        /// <param name="accountId">your basecamp account id</param>
        /// <param name="accessTokenSource">provides access tokens to use with the API</param>
        /// <exception cref="ArgumentNullException">if <paramref name="accessTokenSource"/> is null</exception>
        public BasecampClient(int accountId, IAccessTokenSource accessTokenSource)
            : this(new BasecampClientOptions(accountId, accessTokenSource)) { }

        /// <summary>
        /// Constructs a client from the specified options.
        /// </summary>
        /// <param name="options">the options for connecting to the basecamp API</param>
        /// <exception cref="ArgumentNullException">if <paramref name="options"/> is null</exception>
        public BasecampClient(BasecampClientOptions options) : this(
                apiUri: options.ApiUri,
                accessTokenSource: options.AccessTokenSource,
                rateLimiter: options.RateLimiter,
                responseMessageCache: options.ResponseMessageCache,
                userAgent: options.UserAgent,
                httpClientPool: options.HttpClientPool
            )
        { }

        private BasecampClient(
                Uri apiUri,
                IAccessTokenSource accessTokenSource,
                IRateLimiter rateLimiter,
                IResponseMessageCache responseMessageCache,
                string userAgent,
                HttpClientPool httpClientPool
            )
        {
            if (apiUri == null) throw new ArgumentNullException(nameof(apiUri));
            if (accessTokenSource == null) throw new ArgumentNullException(nameof(accessTokenSource));

            this.apiUri = UriUtils.EnsureTrailingSlash(apiUri);
            this.accessTokenSource = accessTokenSource;
            this.rateLimiter = rateLimiter ?? ConstantRateLimiter.Default;
            this.responseMessageCache = responseMessageCache;
            this.userAgent = userAgent ?? UserAgent.GenerateDefault();
            this.httpClientPool = httpClientPool ?? HttpClientPool.Default;
        }

        public Task<IPageCollection<ProjectResponse>> GetProjectsAsync(ProjectStatusEnum? status = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return GetProjectsAsync(
                status: EnumSerializer.Serialize(status),
                cancellationToken: cancellationToken
            );
        }

        public Task<IPageCollection<ProjectResponse>> GetProjectsAsync(string status, CancellationToken cancellationToken = default(CancellationToken))
        {
            var requestUri = UriUtils.SetQuery(new Uri(apiUri, "projects.json"), new Dictionary<string, string>
            {
                ["status"] = status
            });
            return GetPageCollectionAsync<ProjectResponse>(requestUri, cancellationToken);
        }

        public Task<IPageCollection<ProjectResponse>> GetMoreProjectsAsync(Uri nextUri, CancellationToken cancellationToken = default(CancellationToken))
        {
            return GetPageCollectionAsync<ProjectResponse>(nextUri, cancellationToken);
        }

        public Task<IPageCollection<RecordingResponse>> GetRecordingsAsync(
            RecordingTypeEnum type,
            int? bucket,
            RecordingStatusEnum? status = null,
            RecordingSortEnum? sort = null,
            SortDirection? direction = null, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            IReadOnlyCollection<int> buckets = bucket.HasValue ? new int[] { bucket.Value } : null;
            return GetRecordingsAsync(
                type: type,
                buckets: buckets,
                status: status,
                sort: sort,
                direction: direction, 
                cancellationToken: cancellationToken
            );
        }

        public Task<IPageCollection<RecordingResponse>> GetRecordingsAsync(
            RecordingTypeEnum type,
            IReadOnlyCollection<int> buckets = null,
            RecordingStatusEnum? status = null,
            RecordingSortEnum? sort = null,
            SortDirection? direction = null, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var bucketString = buckets != null ? string.Join(",", buckets) : null;
            return GetRecordingsAsync(
                type: EnumSerializer.Serialize(type),
                bucket: bucketString,
                status: EnumSerializer.Serialize(status),
                sort: EnumSerializer.Serialize(sort),
                direction: EnumSerializer.Serialize(direction),
                cancellationToken: cancellationToken
            );
        }

        public Task<IPageCollection<RecordingResponse>> GetRecordingsAsync(
            string type,
            string bucket = null,
            string status = null,
            string sort = null,
            string direction = null,
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            var requestUri = UriUtils.SetQuery(new Uri(apiUri, "projects/recordings.json"), new Dictionary<string, string>
            {
                ["type"] = type,
                ["bucket"] = bucket,
                ["status"] = status,
                ["sort"] = sort,
                ["direction"] = direction
            });
            return GetPageCollectionAsync<RecordingResponse>(requestUri, cancellationToken);
        }

        public Task<IPageCollection<RecordingResponse>> GetMoreRecordingsAsync(Uri nextUri, CancellationToken cancellationToken = default(CancellationToken))
        {
            return GetPageCollectionAsync<RecordingResponse>(nextUri, cancellationToken);
        }

        public Task<IPageCollection<EventResponse>> GetEventsAsync(long bucketId, long recordingId, CancellationToken cancellationToken = default(CancellationToken))
        {
            var requestUri = new Uri(apiUri, $"buckets/{bucketId}/recordings/{recordingId}/events.json");
            return GetPageCollectionAsync<EventResponse>(requestUri, cancellationToken);
        }

        public Task<IPageCollection<EventResponse>> GetMoreEventsAsync(Uri nextUri, CancellationToken cancellationToken = default(CancellationToken))
        {
            return GetPageCollectionAsync<EventResponse>(nextUri, cancellationToken);
        }

        #region Helpers

        private async Task<IPageCollection<T>> GetPageCollectionAsync<T>(Uri uri, CancellationToken cancellationToken)
        {
            var accessToken = await accessTokenSource.GetAccessTokenAsync();
            try
            {
                return await GetPageCollectionAsync<T>(uri, accessToken, cancellationToken);
            }
            catch (BasecampUnauthorizedResponseException)
            {
                accessToken = await accessTokenSource.GetAccessTokenAsync(forceRefresh: true);
                return await GetPageCollectionAsync<T>(uri, accessToken, cancellationToken);
            }
            catch (BasecampTooManyRequestsException ex)
            {
                var retryAfter = ex.RetryAfter;
                if (!retryAfter.HasValue || retryAfter.Value <= TimeSpan.Zero)
                {
                    throw;
                }
                await Task.Delay(retryAfter.Value);
                return await GetPageCollectionAsync<T>(uri, accessToken, cancellationToken);
            }
        }

        private async Task<IPageCollection<T>> GetPageCollectionAsync<T>(Uri uri, string accessToken, CancellationToken cancellationToken)
        {
            IResponseMessageCacheEntry responseMessageCacheEntry = null;
            if (responseMessageCache != null)
            {
                responseMessageCacheEntry = await responseMessageCache.GetAsync(uri);
            }

            await rateLimiter.WaitIfNecessaryAsync();

            string responseJson;
            Uri nextUri;

            using (var requestMessage = CreateRequest(HttpMethod.Get, uri, accessToken: accessToken, responseMessageCacheEntry: responseMessageCacheEntry))
            using (var httpClient = httpClientPool.Create())
            using (var responseMessage = await httpClient.SendAsync(requestMessage, cancellationToken))
            {
                var localResponseMessage = responseMessage;
                if (responseMessage.StatusCode == System.Net.HttpStatusCode.NotModified && responseMessageCacheEntry != null)
                {
                    localResponseMessage = responseMessageCacheEntry.ToResponseMessage();
                }
                else
                {
                    await HttpResponseUtils.EnsureSuccessAsync(responseMessage);
                }
                responseJson = await localResponseMessage.Content.ReadAsStringAsync();
                nextUri = HttpResponseUtils.ParseNextUriOrNull(localResponseMessage);

                if (responseMessageCacheEntry == null)
                {
                    await CacheResponseMessageAsync(uri, localResponseMessage, responseJson);
                }
            }

            var elements = JsonConvert.DeserializeObject<IReadOnlyCollection<T>>(responseJson);
            return new PageCollection<T>(elements, nextUri, this);
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, Uri requestUri, string accessToken, IResponseMessageCacheEntry responseMessageCacheEntry)
        {
            var requestMessage = new HttpRequestMessage(method, requestUri);
            requestMessage.Headers.UserAgent.TryParseAdd(userAgent);
            if (accessToken != null)
            {
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
            if (responseMessageCache != null)
            {
                if (!string.IsNullOrWhiteSpace(responseMessageCacheEntry.ETag))
                {
                    requestMessage.Headers.IfNoneMatch.TryParseAdd(responseMessageCacheEntry.ETag);
                }
                if (responseMessageCacheEntry.LastModified.HasValue)
                {
                    requestMessage.Headers.IfModifiedSince = responseMessageCacheEntry.LastModified;
                }
            }
            return requestMessage;
        }

        private async Task CacheResponseMessageAsync(Uri uri, HttpResponseMessage responseMessage, string content)
        {
            if (responseMessageCache == null) return;

            var responseMessageCacheEntry = ResponseMessageCacheEntry.CreateOrNull(responseMessage, content);
            if (responseMessageCacheEntry == null)
            {
                return;
            }
            await responseMessageCache.SetAsync(uri, responseMessageCacheEntry);
        }

        #endregion

        #region Inner Classes

        private class PageCollection<T> : IPageCollection<T>
        {
            private readonly IReadOnlyCollection<T> elements;
            private readonly BasecampClient client;

            public PageCollection(IReadOnlyCollection<T> elements, Uri nextUri, BasecampClient client)
            {
                if (elements == null) throw new ArgumentNullException(nameof(elements));
                if (client == null) throw new ArgumentNullException(nameof(client));

                this.elements = elements;
                NextUri = nextUri;
                this.client = client;
            }

            public int Count => elements.Count;

            public IEnumerator<T> GetEnumerator() => elements.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)elements).GetEnumerator();

            public Uri NextUri { get; }

            public Task<IPageCollection<T>> ReadNextAsync(CancellationToken cancellationToken)
            {
                if (NextUri == null)
                {
                    return Task.FromResult<IPageCollection<T>>(new EmptyPageCollection<T>());
                }
                return client.GetPageCollectionAsync<T>(NextUri, cancellationToken);
            }

            public IAsyncEnumerable<T> AsReadToEndEnumerable() => new PageCollectionReadToEndEnumerable<T>(this);

            public IAsyncEnumerator<T> GetReadToEndEnumerator() => new PageCollectionReadToEndEnumerator<T>(this);
        }

        private class PageCollectionReadToEndEnumerable<T> : IAsyncEnumerable<T>
        {
            private readonly IPageCollection<T> pageCollection;

            public PageCollectionReadToEndEnumerable(IPageCollection<T> pageCollection)
            {
                if (pageCollection == null) throw new ArgumentNullException(nameof(pageCollection));

                this.pageCollection = pageCollection;
            }

            public IAsyncEnumerator<T> GetAsyncEnumerator() => new PageCollectionReadToEndEnumerator<T>(pageCollection);
            public IEnumerator<T> GetEnumerator() => GetAsyncEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetAsyncEnumerator();
        }

        private class PageCollectionReadToEndEnumerator<T> : IAsyncEnumerator<T>
        {
            private readonly IPageCollection<T> initialPageCollection;
            private IPageCollection<T> currentPageCollection;
            private IEnumerator<T> currentEnumerator;

            public PageCollectionReadToEndEnumerator(IPageCollection<T> pageCollection)
            {
                if (pageCollection == null) throw new ArgumentNullException(nameof(pageCollection));

                this.initialPageCollection = pageCollection;
                this.currentPageCollection = pageCollection;
                this.currentEnumerator = pageCollection.GetEnumerator();
            }

            public T Current => currentEnumerator.Current;
            object IEnumerator.Current => ((IEnumerator)currentEnumerator).Current;

            public bool MoveNext()
            {
                return MoveNextAsync(CancellationToken.None).GetAwaiter().GetResult();
            }

            public async Task<bool> MoveNextAsync(CancellationToken cancellationToken)
            {
                if (currentPageCollection == null)
                {
                    return false;
                }
                if (currentEnumerator.MoveNext())
                {
                    return true;
                }
                if (currentPageCollection.NextUri == null)
                {
                    return false;
                }

                currentPageCollection = await currentPageCollection.ReadNextAsync(cancellationToken).ConfigureAwait(false);
                if (currentPageCollection == null)
                {
                    return false;
                }

                currentEnumerator.Dispose();
                currentEnumerator = currentPageCollection.GetEnumerator();
                return currentEnumerator.MoveNext();
            }

            public void Reset()
            {
                currentEnumerator.Dispose();
                currentEnumerator = initialPageCollection.GetEnumerator();
            }

            public void Dispose()
            {
                currentEnumerator.Dispose();
            }
        }

        private sealed class EmptyPageCollection<T> : IPageCollection<T>
        {
            int IReadOnlyCollection<T>.Count => 0;
            IEnumerator<T> IEnumerable<T>.GetEnumerator() => Enumerable.Empty<T>().GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Enumerable.Empty<T>()).GetEnumerator();
            Uri IPageCollection<T>.NextUri => null;
            Task<IPageCollection<T>> IPageCollection<T>.ReadNextAsync(CancellationToken cancellationToken) => Task.FromResult<IPageCollection<T>>(this);
            IAsyncEnumerable<T> IPageCollection<T>.AsReadToEndEnumerable() => new PageCollectionReadToEndEnumerable<T>(this);
            IAsyncEnumerator<T> IPageCollection<T>.GetReadToEndEnumerator() => new PageCollectionReadToEndEnumerator<T>(this);
        }

        #endregion
    }

    public sealed class BasecampClientOptions
    {
        public BasecampClientOptions(int accountId, IAccessTokenSource accessTokenSource)
            : this(new Uri($"https://3.basecampapi.com/{accountId}/"), accessTokenSource) { }

        public BasecampClientOptions(Uri apiUri, IAccessTokenSource accessTokenSource)
        {
            if (apiUri == null) throw new ArgumentNullException(nameof(apiUri));
            if (accessTokenSource == null) throw new ArgumentNullException(nameof(accessTokenSource));

            ApiUri = apiUri;
            AccessTokenSource = accessTokenSource;
        }

        public Uri ApiUri { get; }
        public IAccessTokenSource AccessTokenSource { get; }
        public IRateLimiter RateLimiter { get; set; }
        public IResponseMessageCache ResponseMessageCache { get; set; }
        public string UserAgent { get; set; }
        internal HttpClientPool HttpClientPool { get; set; }
    }
}
