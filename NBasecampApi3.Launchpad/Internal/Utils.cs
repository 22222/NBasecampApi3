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

namespace NBasecampApi3.Launchpad
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

            // We might want the response string for the error message.
            // But we can't get it after we call response.EnsureSuccessStatusCode()
            // because that will dispose the content.
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
                // Even though we already know that the code isn't valid, we'll call this so we get 
                // the standard .NET exception in case that has any useful information in it.
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
