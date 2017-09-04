using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NBasecampApi3
{
    /// <summary>
    /// Provides access tokens for authentication with an api.
    /// </summary>
    public interface IAccessTokenSource
    {
        /// <summary>
        /// Returns the current access token, or null if the user is not signed in.
        /// </summary>
        /// <returns>the access token or null</returns>
        Task<string> GetAccessTokenAsync();

        /// <summary>
        /// Returns the current access token, or null if the user is not signed in.
        /// </summary>
        /// <param name="forceRefresh">true to force a new token to be generated (if possible)</param>
        /// <returns>the access token or null</returns>
        Task<string> GetAccessTokenAsync(bool forceRefresh);

    }

    /// <summary>
    /// A simple implementation of <see cref="IAccessTokenSource"/>.
    /// </summary>
    public class SimpleAccessTokenSource : IAccessTokenSource
    {
        /// <summary>
        /// Constructs an access token source that returns the specified token.
        /// </summary>
        public SimpleAccessTokenSource(string accessToken)
        {
            AccessToken = accessToken;
        }

        /// <summary>
        /// The access token to use for all authentication with the API.
        /// </summary>
        public string AccessToken { get; set; }

        Task<string> IAccessTokenSource.GetAccessTokenAsync() => Task.FromResult(AccessToken);
        Task<string> IAccessTokenSource.GetAccessTokenAsync(bool forceRefresh) => Task.FromResult(AccessToken);
    }

    /// <summary>
    /// The default implementation of <see cref="IAccessTokenSource"/> for basecamp.
    /// </summary>
    public class AccessTokenSource : IAccessTokenSource
    {
        private readonly Uri tokenUri;
        private readonly OAuthOptions oauthOptions;
        private readonly string refreshToken;
        private readonly string userAgent;
        private readonly HttpClientPool httpClientPool;

        private string accessToken;
        private DateTime estimatedAccessTokenExpirationUtc;

        /// <summary>
        /// Constructs an access token source from the given values.
        /// </summary>
        public AccessTokenSource(string clientId, string clientSecret, string redirectUrl, string refreshToken)
            : this (new OAuthOptions(clientId, clientSecret, redirectUrl), refreshToken) { }

        /// <summary>
        /// Constructs an access token source from the given <paramref name="oauthOptions"/> and <paramref name="refreshToken"/>.
        /// </summary>
        public AccessTokenSource(OAuthOptions oauthOptions, string refreshToken)
            : this(new AccessTokenSourceOptions(oauthOptions, refreshToken)) { }

        /// <summary>
        /// Constructs an access token source from the given <paramref name="options"/>
        /// </summary>
        public AccessTokenSource(AccessTokenSourceOptions options)
            : this(options.TokenUri, options.OAuthOptions, options.RefreshToken, options.UserAgent, options.HttpClientPool) { }

        private AccessTokenSource(Uri tokenUri, OAuthOptions oauthOptions, string refreshToken, string userAgent, HttpClientPool httpClientPool)
        {
            if (tokenUri == null) throw new ArgumentNullException(nameof(tokenUri));
            if (oauthOptions == null) throw new ArgumentNullException(nameof(oauthOptions));
            if (refreshToken == null) throw new ArgumentNullException(nameof(refreshToken));

            this.tokenUri = tokenUri;
            this.oauthOptions = oauthOptions;
            this.refreshToken = refreshToken;
            this.userAgent = userAgent ?? UserAgent.GenerateDefault();
            this.httpClientPool = httpClientPool ?? HttpClientPool.Default;
        }

        /// <summary>
        /// The refresh token used by this access token source.
        /// </summary>
        public string RefreshToken => refreshToken;

        /// <summary>
        /// Loads a <see cref="TokenResponse"/>.  Use this if you already have an access token you want to use.
        /// </summary>
        /// <param name="tokenResponse">the token response to use</param>
        /// <param name="responseTimeUtc">the time that the response was created to use for calculating the expiration time, or null to default to the current time</param>
        public void LoadToken(TokenResponse tokenResponse, DateTime? responseTimeUtc = null)
        {
            if (tokenResponse == null) throw new ArgumentNullException(nameof(tokenResponse));

            this.accessToken = tokenResponse.AccessToken;
            this.estimatedAccessTokenExpirationUtc = ParseExpiresIn(tokenResponse.ExpiresIn, responseTimeUtc ?? DateTime.UtcNow);
        }

        /// <inheritdoc />
        public Task<string> GetAccessTokenAsync()
        {
            return GetAccessTokenAsync(forceRefresh: false);
        }

        /// <inheritdoc />
        public async Task<string> GetAccessTokenAsync(bool forceRefresh)
        {
            string result = null;
            if (!forceRefresh && !IsRefreshNeeded())
            {
                result = accessToken;
            }
            if (result == null)
            {
                result = await RefreshAsync();
            }
            return result;
        }

        private bool IsRefreshNeeded(DateTime? now = null) => accessToken == null || estimatedAccessTokenExpirationUtc < (now ?? DateTime.UtcNow);

        private async Task<string> RefreshAsync()
        {
            string responseJson;
            DateTime tokenCreateTimeUtc;
            using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, tokenUri))
            {
                requestMessage.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["type"] = "refresh",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = oauthOptions.ClientId,
                    ["redirect_uri"] = oauthOptions.RedirectUrl,
                    ["client_secret"] = oauthOptions.ClientSecret,
                });
                requestMessage.Headers.UserAgent.TryParseAdd(userAgent);

                using (var httpClient = httpClientPool.Create())
                using (var responseMessage = await httpClient.SendAsync(requestMessage))
                {
                    await HttpResponseUtils.EnsureSuccessAsync(responseMessage);
                    tokenCreateTimeUtc = DateTime.UtcNow;
                    responseJson = await responseMessage.Content.ReadAsStringAsync();
                }
            }

            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseJson);
            this.accessToken = tokenResponse.AccessToken;
            this.estimatedAccessTokenExpirationUtc = ParseExpiresIn(tokenResponse.ExpiresIn, tokenCreateTimeUtc);
            return accessToken;
        }

        private static DateTime ParseExpiresIn(long expiresInSeconds, DateTime createTimeUtc)
        {
            if (expiresInSeconds > 15)
            {
                // Remove a little time to account for any delays or clock differences or whatever.
                expiresInSeconds -= 10;
            }
            return createTimeUtc.AddSeconds(expiresInSeconds);
        }
    }

    public class AccessTokenSourceOptions
    {
        public AccessTokenSourceOptions(OAuthOptions oauthOptions, string refreshToken)
            : this(new Uri($"https://launchpad.37signals.com/authorization/token"), oauthOptions, refreshToken) { }

        public AccessTokenSourceOptions(Uri tokenUri, OAuthOptions oauthOptions, string refreshToken)
        {
            if (tokenUri == null) throw new ArgumentNullException(nameof(tokenUri));
            if (oauthOptions == null) throw new ArgumentNullException(nameof(oauthOptions));

            TokenUri = tokenUri;
            OAuthOptions = oauthOptions;
            RefreshToken = refreshToken;
        }

        public Uri TokenUri { get; }
        public OAuthOptions OAuthOptions { get; }
        public string RefreshToken { get; }
        public string UserAgent { get; set; }
        internal HttpClientPool HttpClientPool { get; set; }
    }

    /// <summary>
    /// Configuration values for making authorization requests to the basecamp API.
    /// </summary>
    public class OAuthOptions
    {
        public OAuthOptions(string clientId, string clientSecret, string redirectUrl)
        {
            ClientId = clientId;
            ClientSecret = clientSecret;
            RedirectUrl = redirectUrl;
        }

        public string ClientId { get; }
        public string ClientSecret { get; }
        public string RedirectUrl { get; }
    }
}
