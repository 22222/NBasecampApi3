using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NBasecampApi3.Launchpad
{
    public class LaunchpadClient
    {
        static LaunchpadClient()
        {
            System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
        }

        private readonly Uri launchpadUri;
        private readonly OAuthOptions oauthOptions;
        private readonly string userAgent;
        private readonly HttpClientPool httpClientPool;

        public LaunchpadClient(OAuthOptions oauthOptions)
            : this(new LaunchpadOptions(oauthOptions)) { }

        public LaunchpadClient(LaunchpadOptions options)
            : this(options.LaunchpadUri, options.OAuthOptions, options.UserAgent, options.HttpClientPool) { }

        private LaunchpadClient(Uri launchpadUri, OAuthOptions oauthOptions, string userAgent, HttpClientPool httpClientPool)
        {
            if (launchpadUri == null) throw new ArgumentNullException(nameof(launchpadUri));
            if (oauthOptions == null) throw new ArgumentNullException(nameof(oauthOptions));

            this.launchpadUri = UriUtils.EnsureTrailingSlash(launchpadUri);
            this.oauthOptions = oauthOptions;
            this.userAgent = userAgent ?? UserAgent.GenerateDefault();
            this.httpClientPool = httpClientPool ?? HttpClientPool.Default;
        }

        public Task<AccessTokenSource> AuthenticateCookieAsync(LaunchpadCookie basecampCookie, string username)
        {
            return AuthenticateCookieAsync(basecampCookie, username: username);
        }

        public async Task<AccessTokenSource> AuthenticateCookieAsync(string basecampCookieString, string username)
        {
            // We probably need an authenticity token from what's supposed to be the page before this one.
            var authenticityToken = await GetAuthenticityTokenAsync(
                requestUri: new Uri(launchpadUri, $"authorization/new?type=web_server&client_id={Uri.EscapeDataString(oauthOptions.ClientId)}&redirect_uri={oauthOptions.RedirectUrl}"),
                basecampCookieString: basecampCookieString
            );

            string verificationCode;

            var requestUri = new Uri(launchpadUri, "authorization");
            using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri))
            {
                requestMessage.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["utf8"] = "✓",
                    ["authenticity_token"] = authenticityToken,
                    ["client_id"] = oauthOptions.ClientId,
                    ["client_secret"] = "",
                    ["type"] = "web_server",
                    ["state"] = "",
                    ["redirect_uri"] = oauthOptions.RedirectUrl,
                    ["commit"] = ""
                });

                InitializeBrowserRequest(requestMessage);
                requestMessage.Headers.Referrer = new Uri(launchpadUri, $"signin?login_hint={Uri.EscapeDataString(username)}");
                requestMessage.Headers.TryAddWithoutValidation("Cookie", basecampCookieString);
                using (var httpClient = httpClientPool.Create(new HttpClientHandlerOptions(allowAutoRedirect: false)))
                using (var responseMessage = await httpClient.SendAsync(requestMessage))
                {
                    await HttpResponseUtils.EnsureSuccessAsync(responseMessage, allowRedirect: true);
                    if (responseMessage.StatusCode != System.Net.HttpStatusCode.Redirect)
                    {
                        throw new BasecampResponseException($"Expected a redirect for authorization/new request but was {responseMessage.StatusCode}")
                        {
                            RequestUri = requestUri,
                            ResponseStatusCode = responseMessage.StatusCode,
                        };
                    }

                    var redirectLocation = responseMessage.Headers.Location;
                    if (redirectLocation == null)
                    {
                        throw new BasecampResponseException($"Expected a redirect location for authorization/new request")
                        {
                            RequestUri = requestUri,
                            ResponseStatusCode = responseMessage.StatusCode,
                        };
                    }

                    // Location: urn:ietf:wg:oauth:2.0:oob?code=de615fc1
                    var queryStringPairs = ParseQueryString(redirectLocation.Query);
                    verificationCode = queryStringPairs.Where(kv => kv.Key == "code").Select(kv => kv.Value).FirstOrDefault();
                    if (verificationCode == null)
                    {
                        throw new BasecampResponseException($"Expected verification code for authorization/new request but url was <{redirectLocation}>")
                        {
                            RequestUri = requestUri,
                            ResponseStatusCode = responseMessage.StatusCode,
                        };
                    }
                }
            }

            return await AuthenticateVerificationCodeAsync(verificationCode);
        }

        private async Task<string> GetAuthenticityTokenAsync(Uri requestUri, string basecampCookieString)
        {
            string authenticityToken;

            using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri))
            {
                InitializeBrowserRequest(requestMessage);
                requestMessage.Headers.TryAddWithoutValidation("Cookie", basecampCookieString);

                using (var httpClient = httpClientPool.Create(new HttpClientHandlerOptions()))
                using (var responseMessage = await httpClient.SendAsync(requestMessage))
                {
                    await HttpResponseUtils.EnsureSuccessAsync(responseMessage);

                    var responseHtml = await responseMessage.Content.ReadAsStringAsync();
                    authenticityToken = ParseInputValue(responseHtml, "authenticity_token");
                }
            }

            return authenticityToken;
        }

        private static string ParseInputValue(string html, string name)
        {
            string inputValue = null;

            // We could bring in a real HTML parser, but that's overkill if this is the only value we need.
            // This might be a little brittle if they change how their login page works, making assumptions like 
            // that they're be using double quotes and the thing will be in an HTML input.

            // Looking for something like this:
            // <input type="hidden" name="authenticity_token" value="vcj+uW5bP94Ln7LcfpER4xs91hWL/V+5UHVOxtAzcvg0BeDOccZBVJp4RNTuS35hnUOMc/JQwVpdBdsIlyiPTA==" />

            var inputMatches = Regex.Matches(html, "<input [^>]+>", RegexOptions.IgnoreCase);
            foreach (var inputMatch in inputMatches.OfType<Match>())
            {
                if (!inputMatch.Success) continue;

                var inputHtml = inputMatch.Value;


                var nameMatch = Regex.Match(inputHtml, @"name\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (!nameMatch.Success || nameMatch.Groups[1].Value.ToLowerInvariant() != name)
                {
                    continue;
                }

                var valueMatch = Regex.Match(inputHtml, @"value\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (valueMatch.Success)
                {
                    inputValue = valueMatch.Groups[1].Value;
                    break;
                }
            }
            return inputValue;
        }

        public async Task<AccessTokenSource> AuthenticateVerificationCodeAsync(string verificationCode)
        {
            string responseJson;

            var requestUri = new Uri(launchpadUri, "authorization/token");
            using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri))
            {
                requestMessage.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["type"] = "web_server",
                    ["client_id"] = oauthOptions.ClientId,
                    ["redirect_uri"] = oauthOptions.RedirectUrl,
                    ["client_secret"] = oauthOptions.ClientSecret,
                    ["code"] = verificationCode
                });
                requestMessage.Headers.UserAgent.TryParseAdd(userAgent);

                using (var httpClient = httpClientPool.Create())
                using (var responseMessage = await httpClient.SendAsync(requestMessage))
                {
                    await HttpResponseUtils.EnsureSuccessAsync(responseMessage);
                    responseJson = await responseMessage.Content.ReadAsStringAsync();
                }
            }

            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseJson);
            var accessTokenSourceOptions = new AccessTokenSourceOptions(
                tokenUri: new Uri(launchpadUri, "authorization/token"), 
                oauthOptions: oauthOptions, 
                refreshToken: tokenResponse.RefreshToken
            );
            var accessTokenSource = new AccessTokenSource(accessTokenSourceOptions);
            accessTokenSource.LoadToken(tokenResponse);
            return accessTokenSource;
        }

        public async Task<AuthorizationResponse> GetAuthorizationAsync(string accessToken)
        {
            string responseJson;

            var requestUri = new Uri(launchpadUri, "authorization.json");
            using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri))
            {
                requestMessage.Headers.UserAgent.TryParseAdd(userAgent);
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                using (var httpClient = httpClientPool.Create())
                using (var responseMessage = await httpClient.SendAsync(requestMessage))
                {
                    await HttpResponseUtils.EnsureSuccessAsync(responseMessage);
                    responseJson = await responseMessage.Content.ReadAsStringAsync();
                }
            }

            return JsonConvert.DeserializeObject<AuthorizationResponse>(responseJson);
        }

        #region Helpers

        private void InitializeBrowserRequest(HttpRequestMessage requestMessage)
        {
            // We need to pretend that we're a real browser.
            // So include all of the request headers that a real browser would include.

            // Accept:text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8
            // Accept-Encoding:gzip, deflate, br
            // Accept-Language:en-US,en;q=0.8
            // Connection:keep-alive
            // Host:launchpad.37signals.com
            // If-None-Match:W/"416cb541d837281bfcbe440d9b87ee8d"
            // Upgrade-Insecure-Requests:1
            // User-Agent:Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.90 Safari/537.36

            requestMessage.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
            requestMessage.Headers.AcceptEncoding.ParseAdd("gzip, deflate, br");
            requestMessage.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.8");
            requestMessage.Headers.Connection.ParseAdd("keep-alive");
            requestMessage.Headers.Host = "launchpad.37signals.com";
            requestMessage.Headers.UserAgent.ParseAdd(@"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.90 Safari/537.36");
        }

        private static IEnumerable<KeyValuePair<string, string>> ParseQueryString(string queryString)
        {
            queryString = queryString?.TrimStart('?');
            if (string.IsNullOrWhiteSpace(queryString))
            {
                yield break;
            }

            var segments = queryString.Split('&');
            foreach (string segment in segments)
            {
                string[] parts = segment.Split('=');
                var key = parts.ElementAt(0);
                var value = parts.ElementAtOrDefault(1);
                yield return new KeyValuePair<string, string>(key, value);
            }
        }

        #endregion
    }

    public class LaunchpadOptions
    {
        public LaunchpadOptions(OAuthOptions oauthOptions)
            : this(new Uri("https://launchpad.37signals.com/"), oauthOptions) { }

        public LaunchpadOptions(Uri launchpadUri, OAuthOptions oauthOptions)
        {
            if (launchpadUri == null) throw new ArgumentNullException(nameof(launchpadUri));
            if (oauthOptions == null) throw new ArgumentNullException(nameof(oauthOptions));

            LaunchpadUri = launchpadUri;
            OAuthOptions = oauthOptions;
        }

        public Uri LaunchpadUri { get; }
        public OAuthOptions OAuthOptions { get; }
        public string UserAgent { get; set; }
        internal HttpClientPool HttpClientPool { get; set; }
    }
}
