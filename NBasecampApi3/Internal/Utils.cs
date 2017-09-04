using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace NBasecampApi3
{
    internal static class UriUtils
    {
        public static Uri EnsureTrailingSlash(Uri uri)
        {
            var url = uri.AbsoluteUri;
            if (!url.EndsWith("/"))
            {
                uri = new Uri(url + "/");
            }
            return uri;
        }

        public static Uri SetQuery(Uri uri, IEnumerable<KeyValuePair<string, string>> parameters)
        {
            if (parameters == null || !parameters.Any())
            {
                return uri;
            }

            var parameterStrings = parameters
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}");
            var queryString = string.Join("&", parameterStrings);
            if (string.IsNullOrWhiteSpace(queryString))
            {
                return uri;
            }

            var uriBuilder = new UriBuilder(uri);
            uriBuilder.Query = queryString;
            return uriBuilder.Uri;
        }
    }

    internal static class HttpResponseUtils
    {
        public static async Task EnsureSuccessAsync(HttpResponseMessage responseMessage, bool allowRedirect = false)
        {
            if (responseMessage.IsSuccessStatusCode)
            {
                return;
            }
            if (allowRedirect && responseMessage.StatusCode == System.Net.HttpStatusCode.Redirect)
            {
                return;
            }

            // We know there's an error at this point, so we need to read the 
            // response message content so we can parse out the error message.
            string responseString = null;
            string responseContentType = null;
            if (responseMessage.Content != null)
            {
                try
                {
                    responseString = await responseMessage.Content.ReadAsStringAsync();
                    responseContentType = responseMessage.Content.Headers.ContentType.MediaType;
                }
                catch (Exception)
                {
                    responseString = null;
                }
            }

            try
            {
                // Even though we already know that the response isn't valid, 
                // we'll call this so we get the standard .NET exception in 
                // case that has any useful information in it.
                responseMessage.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                string errorMessage;
                if (responseContentType == "text/plain")
                {
                    errorMessage = responseString;
                }
                else if (responseContentType == "application/json")
                {
                    errorMessage = ParseErrorMessageOrNull(responseString);
                }
                else
                {
                    errorMessage = null;
                }
                errorMessage = errorMessage ?? ex.Message;

                BasecampResponseException exception;
                if (responseMessage.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    exception = new BasecampUnauthorizedResponseException(errorMessage, ex);
                }
                else if (responseMessage.StatusCode == (System.Net.HttpStatusCode)429)
                {
                    exception = new BasecampTooManyRequestsException(errorMessage, ex)
                    {
                        RetryAfter = ParseRetryAfterOrNull(responseMessage)
                    };
                }
                else
                {
                    exception = new BasecampResponseException(errorMessage, ex);
                }
                exception.RequestUri = responseMessage.RequestMessage?.RequestUri;
                exception.ResponseStatusCode = responseMessage.StatusCode;
                exception.ResponseContent = responseString;
                throw exception;
            }

        }

        private static string ParseErrorMessageOrNull(string json)
        {
            string errorMessage = null;
            try
            {
                var jsonDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (errorMessage == null && jsonDictionary.ContainsKey("error"))
                {
                    errorMessage = jsonDictionary["error"] as string;
                }
            }
            catch (Exception ex)
            {
                errorMessage = null;
            }
            return errorMessage;
        }

        public static Uri ParseNextUriOrNull(HttpResponseMessage responseMessage)
        {
            return ParseNextUriOrNull(responseMessage?.Headers);
        }

        public static Uri ParseNextUriOrNull(IEnumerable<KeyValuePair<string, IEnumerable<string>>> responseHeaders)
        {
            if (responseHeaders == null) return null;

            IEnumerable<string> linkHeaderValues;
            if (responseHeaders is HttpHeaders)
            {
                var httpHeaders = (HttpHeaders)responseHeaders;
                if (!httpHeaders.TryGetValues("Link", out linkHeaderValues))
                {
                    linkHeaderValues = Enumerable.Empty<string>();
                }
            }
            else
            {
                linkHeaderValues = responseHeaders.Where(kv => kv.Key == "Link").SelectMany(kv => kv.Value);
            }
            return ParseNextUriOrNull(linkHeaderValues);
        }

        private static Uri ParseNextUriOrNull(IEnumerable<string> linkHeaderValues)
        {
            if (linkHeaderValues == null) return null;

            // Link: <https://3.basecampapi.com/999999999/buckets/2085958496/messages.json?page=4>; rel="next"
            foreach (var value in linkHeaderValues)
            {
                var linkHeader = ParseLinkHeader(value);
                if (linkHeader != null && linkHeader.Rel == "next")
                {
                    return linkHeader.Uri;
                }
            }
            return null;
        }

        private static LinkHeader ParseLinkHeader(string value)
        {
            var tokens = value.Split(';');

            var url = tokens.ElementAt(0);
            url = url.Trim().TrimStart('<').TrimEnd('>');

            Uri uri;
            try
            {
                uri = new Uri(url);
            }
            catch (Exception)
            {
                return null;
            }


            var rel = tokens.ElementAtOrDefault(1)?.Trim()?.ToLowerInvariant();
            rel = rel?.Trim().ToLowerInvariant();

            return new LinkHeader(uri, rel);
        }

        private class LinkHeader
        {
            public LinkHeader(Uri uri, string rel)
            {
                Uri = uri;
                Rel = rel;
            }
            public Uri Uri { get; }
            public string Rel { get; }
        }

        public static TimeSpan? ParseRetryAfterOrNull(HttpResponseMessage responseMessage)
        {
            return responseMessage.Headers.RetryAfter?.Delta;
        }
    }

    internal static class DateTimeOffsetConvert
    {
        public static string Serialize(DateTimeOffset? value)
        {
            return value?.ToString("o");
        }

        public static DateTimeOffset? Deserialize(string value)
        {
            DateTimeOffset dto;
            if (!DateTimeOffset.TryParse(value, formatProvider: CultureInfo.InvariantCulture, styles: DateTimeStyles.AssumeUniversal, result: out dto))
            {
                return null;
            }
            return dto;
        }
    }

    internal static class EnumSerializer
    {
        public static string Serialize<TEnum>(TEnum? value)
            where TEnum : struct
        {
            if (!value.HasValue)
            {
                return null;
            }
            return Serialize<TEnum>(value.Value);
        }

        public static string Serialize<TEnum>(TEnum value)
            where TEnum : struct
        {
            var fieldInfo = typeof(TEnum).GetField(value.ToString());
            var descriptionAttributes = fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), inherit: false) as DescriptionAttribute[];
            var description = descriptionAttributes?.FirstOrDefault()?.Description;
            if (description != null)
            {
                return description;
            }
            return Enum.GetName(typeof(TEnum), value);
        }
    }
}
