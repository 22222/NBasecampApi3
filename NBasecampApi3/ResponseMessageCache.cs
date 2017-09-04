using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace NBasecampApi3
{
    /// <summary>
    /// A cache for HTTP response messages.
    /// </summary>
    public interface IResponseMessageCache
    {
        /// <summary>
        /// Returns the cache entry for the given uri or null.
        /// </summary>
        Task<IResponseMessageCacheEntry> GetAsync(Uri requestUri);

        /// <summary>
        /// Sets the cache entry for the given uri.
        /// </summary>
        Task SetAsync(Uri requestUri, IResponseMessageCacheEntry responseMessageCacheEntry);
    }

    /// <summary>
    /// A very basic implementation of <see cref="IResponseMessageCache"/> that only caches one response at a time.
    /// </summary>
    public class SingleResponseMessageCache : IResponseMessageCache
    {
        private KeyValuePair<Uri, ResponseMessageCacheEntry> cacheNode;

        /// <inheritdoc />
        public Task<IResponseMessageCacheEntry> GetAsync(Uri requestUri)
        {
            IResponseMessageCacheEntry result = null;
            if (cacheNode.Key == requestUri)
            {
                result = cacheNode.Value;
            }
            return Task.FromResult(result);
        }

        /// <inheritdoc />
        public async Task SetAsync(Uri requestUri, IResponseMessageCacheEntry responseMessageCacheEntry)
        {
            var cacheValue = await ResponseMessageCacheEntry.WrapAsync(responseMessageCacheEntry);
            cacheNode = new KeyValuePair<Uri, ResponseMessageCacheEntry>(requestUri, cacheValue);
        }
    }

    /// <summary>
    /// An entry in a <see cref="IResponseMessageCache"/>.
    /// </summary>
    public interface IResponseMessageCacheEntry
    {
        /// <summary>
        /// The HTTP ETag value for this cache entry, if any.
        /// </summary>
        string ETag { get; }

        /// <summary>
        /// The last modified value for this cache entry, if any.
        /// </summary>
        DateTimeOffset? LastModified { get; }

        /// <summary>
        /// Returns a response message created from the cache.
        /// </summary>
        HttpResponseMessage ToResponseMessage();
    }

    /// <summary>
    /// A default implementation of <see cref="IResponseMessageCache"/>.
    /// </summary>
    public class ResponseMessageCacheEntry : IResponseMessageCacheEntry
    {
        /// <summary>
        /// Wraps a <see cref="IResponseMessageCacheEntry"/> object as a <see cref="ResponseMessageCacheEntry"/>.
        /// </summary>
        public static async Task<ResponseMessageCacheEntry> WrapAsync(IResponseMessageCacheEntry entry)
        {
            var result = entry as ResponseMessageCacheEntry;
            if (result == null)
            {
                result = await CreateAsync(entry.ETag, entry.LastModified, entry.ToResponseMessage());
            }
            return result;
        }

        /// <summary>
        /// Creates a <see cref="ResponseMessageCacheEntry"/> from a <see cref="HttpResponseMessage"/> and string content.
        /// </summary>
        public static ResponseMessageCacheEntry CreateOrNull(HttpResponseMessage responseMessage, string content)
        {
            if (responseMessage == null || responseMessage.Content == null || content == null) return null;

            var etag = responseMessage.Headers.ETag?.Tag;
            var lastModified = responseMessage.Content.Headers.LastModified;
            if (etag == null && !lastModified.HasValue)
            {
                return null;
            }
            return new ResponseMessageCacheEntry(etag, lastModified, responseMessage, content);
        }

        /// <summary>
        /// Creates a <see cref="ResponseMessageCacheEntry"/> from a <see cref="HttpResponseMessage"/> and content bytes.
        /// </summary>
        public static ResponseMessageCacheEntry CreateOrNull(HttpResponseMessage responseMessage, byte[] contentBytes)
        {
            if (responseMessage == null || responseMessage.Content == null || contentBytes == null) return null;

            var etag = responseMessage.Headers.ETag?.Tag;
            var lastModified = responseMessage.Content.Headers.LastModified;
            if (etag == null && !lastModified.HasValue)
            {
                return null;
            }
            return new ResponseMessageCacheEntry(etag, lastModified, responseMessage, contentBytes);
        }

        /// <summary>
        /// Creates a <see cref="ResponseMessageCacheEntry"/> from a <see cref="HttpResponseMessage"/> asynchronously.
        /// </summary>
        public static Task<ResponseMessageCacheEntry> CreateOrNullAsync(HttpResponseMessage responseMessage)
        {
            var etag = responseMessage.Headers.ETag?.Tag;
            var lastModified = responseMessage.Content.Headers.LastModified;
            if (etag == null && !lastModified.HasValue)
            {
                return null;
            }
            return CreateAsync(etag, lastModified, responseMessage);
        }

        /// <summary>
        /// Creates a <see cref="ResponseMessageCacheEntry"/> from a <see cref="HttpResponseMessage"/> asynchronously.
        /// </summary>
        public static async Task<ResponseMessageCacheEntry> CreateAsync(string etag, DateTimeOffset? lastModified, HttpResponseMessage responseMessage)
        {
            var contentBytes = await responseMessage.Content.ReadAsByteArrayAsync();
            return new ResponseMessageCacheEntry(etag, lastModified, responseMessage, contentBytes);
        }

        /// <summary>
        /// This constructor is primarily intended for serialization purposes, 
        /// so in most cases you'll probably want to use one of the other constructors 
        /// or the static `CreateOrNull` methods.
        /// </summary>
        public ResponseMessageCacheEntry() { }

        /// <summary>
        /// Constructs a cache entry from the specified response message.
        /// </summary>
        /// <param name="etag">the ETag response for this cache entry (if any)</param>
        /// <param name="lastModified">the last modified time for this cache entry (if any)</param>
        /// <param name="responseMessage">the response message to cache</param>
        /// <param name="content">the contents as a string</param>
        /// <exception cref="ArgumentNullException">if <paramref name="responseMessage"/> is null</exception>
        /// <exception cref="InvalidOperationException">if both <paramref name="etag"/> and <paramref name="lastModified"/> are null</exception>
        public ResponseMessageCacheEntry(string etag, DateTimeOffset? lastModified, HttpResponseMessage responseMessage, string content)
            : this(etag, lastModified, responseMessage.StatusCode, responseMessage.Headers, CloneContentHeadersForString(responseMessage.Content.Headers, content), Encoding.UTF8.GetBytes(content)) { }

        private static HttpContentHeaders CloneContentHeadersForString(HttpContentHeaders contentHeaders, string content)
        {
            var newContent = new StringContent(content, Encoding.UTF8);

            // We might be changing the encoding, so we only want to keep the headers that don't have anything to do with the contents.
            newContent.Headers.LastModified = contentHeaders.LastModified;
            newContent.Headers.Expires = contentHeaders.Expires;

            return newContent.Headers;
        }

        /// <summary>
        /// Constructs a cache entry from the specified response message.
        /// </summary>
        /// <param name="etag">the ETag response for this cache entry (if any)</param>
        /// <param name="lastModified">the last modified time for this cache entry (if any)</param>
        /// <param name="responseMessage">the response message to cache</param>
        /// <param name="contentBytes">the contents as a byte array</param>
        /// <exception cref="ArgumentNullException">if <paramref name="responseMessage"/> is null</exception>
        /// <exception cref="InvalidOperationException">if both <paramref name="etag"/> and <paramref name="lastModified"/> are null</exception>
        public ResponseMessageCacheEntry(string etag, DateTimeOffset? lastModified, HttpResponseMessage responseMessage, byte[] contentBytes)
            : this(etag, lastModified, responseMessage.StatusCode, responseMessage.Headers, responseMessage.Content.Headers, contentBytes) { }

        private ResponseMessageCacheEntry(string etag, DateTimeOffset? lastModified, HttpStatusCode statusCode, HttpResponseHeaders responseHeaders, HttpContentHeaders contentHeaders, byte[] contentBytes)
        {
            if (responseHeaders == null) throw new ArgumentNullException(nameof(responseHeaders));
            if (etag == null && !lastModified.HasValue) throw new InvalidOperationException("Must have at least one of an ETag or LastModified value");

            ETag = etag;
            LastModified = lastModified;
            StatusCode = statusCode;
            Headers = responseHeaders.ToDictionary(kv => kv.Key, kv => AsReadOnlyCollection(kv.Value));
            ContentHeaders = contentHeaders.ToDictionary(kv => kv.Key, kv => AsReadOnlyCollection(kv.Value));
            ContentBytes = contentBytes;
        }

        private static IReadOnlyCollection<string> AsReadOnlyCollection(IEnumerable<string> values)
        {
            return values as IReadOnlyCollection<string> ?? values?.ToArray() ?? (IReadOnlyCollection<string>)new string[0];
        }

        /// <inheritdoc />
        public string ETag { get; set; }

        /// <inheritdoc />
        public DateTimeOffset? LastModified { get; set; }

        public HttpStatusCode StatusCode { get; set; }

        public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Headers { get; set; }

        public IReadOnlyDictionary<string, IReadOnlyCollection<string>> ContentHeaders { get; set; }

        public byte[] ContentBytes { get; set; }

        /// <inheritdoc />
        public HttpResponseMessage ToResponseMessage()
        {
            var responseMessage = new HttpResponseMessage(StatusCode);
            if (Headers != null)
            {
                foreach (var headerKv in Headers)
                {
                    foreach (var value in headerKv.Value)
                    {
                        responseMessage.Headers.TryAddWithoutValidation(headerKv.Key, headerKv.Value);
                    }
                }
            }
            if (ContentBytes != null)
            {
                responseMessage.Content = new ByteArrayContent(ContentBytes);
                if (ContentHeaders != null)
                {
                    foreach (var headerKv in ContentHeaders)
                    {
                        foreach (var value in headerKv.Value)
                        {
                            responseMessage.Content.Headers.TryAddWithoutValidation(headerKv.Key, headerKv.Value);
                        }
                    }
                }
            }
            return responseMessage;
        }
    }
}
