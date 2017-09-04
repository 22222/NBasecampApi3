using LatticeObjectTree;
using Newtonsoft.Json;
using NUnit.Framework;
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
    public class ResponseMessageCacheEntryTest
    {
        [Test]
        public async Task CreateOrNullAsync()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);
            responseMessage.Headers.ETag = new EntityTagHeaderValue("\"abcdef==\"");
            responseMessage.Headers.TryAddWithoutValidation("Link", @"<https://3.basecampapi.com/999999999/buckets/2085958496/messages.json?page=4>; rel=""next""");
            responseMessage.Content = new StringContent("This is the content", Encoding.UTF8);
            responseMessage.Content.Headers.LastModified = new DateTimeOffset(2002, 2, 2, 12, 22, 22, TimeSpan.Zero);

            var entry = await ResponseMessageCacheEntry.CreateOrNullAsync(responseMessage);
            Assert.IsNotNull(entry);

            Assert.AreEqual(responseMessage.StatusCode, entry.StatusCode);
            Assert.AreEqual("\"abcdef==\"", entry.ETag);
            Assert.AreEqual(new DateTimeOffset(2002, 2, 2, 12, 22, 22, TimeSpan.Zero), entry.LastModified);
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("This is the content"), entry.ContentBytes);

            CollectionAssert.AreEquivalent(responseMessage.Headers, entry.Headers);
            CollectionAssert.AreEquivalent(responseMessage.Content.Headers, entry.ContentHeaders);
        }

        [Test]
        public async Task SerializeAndDeserialize()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);
            responseMessage.Headers.ETag = new EntityTagHeaderValue("\"abcdef==\"");
            responseMessage.Headers.TryAddWithoutValidation("Link", @"<https://3.basecampapi.com/999999999/buckets/2085958496/messages.json?page=4>; rel=""next""");
            responseMessage.Content = new StringContent("This is the content");

            var entry = await ResponseMessageCacheEntry.CreateOrNullAsync(responseMessage);
            Assert.IsNotNull(entry);
            var serialized = JsonConvert.SerializeObject(entry);
            var deserialized = JsonConvert.DeserializeObject<ResponseMessageCacheEntry>(serialized);

            ObjectTreeAssert.AreEqual(entry, deserialized, new ObjectTreeNodeFilter
            {
                ExcludedPropertyNames = new[] { nameof(ResponseMessageCacheEntry.Headers), nameof(ResponseMessageCacheEntry.ContentHeaders) },
            });

            CollectionAssert.AreEquivalent(entry.Headers, deserialized.Headers);
            CollectionAssert.AreEquivalent(entry.ContentHeaders, deserialized.ContentHeaders);
        }
    }
}
