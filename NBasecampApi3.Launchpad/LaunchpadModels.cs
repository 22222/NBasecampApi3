using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBasecampApi3.Launchpad
{
    public class AuthorizationResponse
    {
        [JsonProperty("expires_at")]
        public string ExpiresAtString { get; set; }

        [JsonIgnore]
        public DateTimeOffset? ExpiresAt
        {
            get { return DateTimeOffsetConvert.Deserialize(ExpiresAtString); }
            set { ExpiresAtString = DateTimeOffsetConvert.Serialize(value); }
        }

        [JsonProperty("identity")]
        public IdentityResponse Identity { get; set; }

        [JsonProperty("accounts")]
        public IReadOnlyCollection<AccountResponse> Accounts { get; set; }
    }

    public class IdentityResponse
    {
        [JsonProperty("id")]
        public int? Id { get; set; }

        [JsonProperty("first_name")]
        public string FirstName { get; set; }

        [JsonProperty("last_name")]
        public string LastName { get; set; }

        [JsonProperty("email_address")]
        public string EmailAddress { get; set; }
    }

    public class AccountResponse
    {
        [JsonProperty("product")]
        public string Product { get; set; }

        [JsonProperty("id")]
        public int? Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("href")]
        public string Href { get; set; }

        [JsonProperty("app_href")]
        public string AppHref { get; set; }
    }

    public class LaunchpadCookie
    {
        public LaunchpadCookie(string sessionToken = null, string rememberMe = null, string launchpadSession = null, IReadOnlyDictionary<string, string> customValues = null)
        {
            SessionToken = sessionToken;
            RememberMe = rememberMe;
            LaunchpadSession = launchpadSession;
            CustomValues = customValues;
        }

        public string SessionToken { get; }
        public string RememberMe { get; }
        public string LaunchpadSession { get; }
        public IReadOnlyDictionary<string, string> CustomValues { get; }

        public override string ToString()
        {
            return string.Join("; ", ToPairs().Select(kv => kv.Value != null ? $"{kv.Key}={kv.Value}" : kv.Key));
        }

        private IEnumerable<KeyValuePair<string, string>> ToPairs()
        {
            if (SessionToken != null)
            {
                yield return new KeyValuePair<string, string>("session_token", SessionToken);
            }
            if (RememberMe != null)
            {
                yield return new KeyValuePair<string, string>("remember_me", RememberMe);
            }
            if (LaunchpadSession != null)
            {
                yield return new KeyValuePair<string, string>("_launchpad_session", LaunchpadSession);
            }

            if (CustomValues != null)
            {
                foreach (var customKv in CustomValues)
                {
                    yield return customKv;
                }
            }
        }
    }

}
