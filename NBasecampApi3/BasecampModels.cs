using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.ComponentModel;
using System.Threading;

namespace NBasecampApi3
{
    /// <summary>
    /// A user agent value to include with API requests.
    /// </summary>
    public class UserAgent
    {
        /// <summary>
        /// Generates a default user agent to use with the basecamp API.
        /// </summary>
        public static string GenerateDefault()
        {
            var assembly = System.Reflection.Assembly.GetAssembly(typeof(UserAgent));
            var version = assembly?.GetName().Version;
            return Generate(
                applicationName: "NBasecampApi3",
                applicationVersion: version,
                contactAddress: null
            );
        }

        /// <summary>
        /// Generates a user agent for the specified application name and contact address, like `AppName (contact@example.com)`.
        /// </summary>
        /// <param name="applicationName">the name of the application (preferably with no spaces)</param>
        /// <param name="contactAddress">a way to contact you (like a website or email)</param>
        public static string Generate(string applicationName, string contactAddress)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly?.GetName().Version;
            return Generate(
                applicationName: applicationName,
                applicationVersion: version,
                contactAddress: contactAddress
            );
        }

        /// <summary>
        /// Generates a user agent for the specified application identifiers and contact address, like `AppName/1.0 (contact@example.com)`.
        /// </summary>
        /// <param name="applicationName">the name of the application (preferably with no spaces)</param>
        /// <param name="applicationVersion">the version of the application</param>
        /// <param name="contactAddress">a way to contact you (like a website or email)</param>
        public static string Generate(string applicationName, Version applicationVersion, string contactAddress)
        {
            var applicationIdentifier = applicationName;
            if (applicationVersion != null)
            {
                var major = applicationVersion.Major;
                var minor = applicationVersion.Minor;
                applicationIdentifier = $"{applicationName}/{major}.{minor}";
            }

            if (!string.IsNullOrWhiteSpace(contactAddress))
            {
                return $"{applicationIdentifier} ({contactAddress})";
            }
            else
            {
                return applicationIdentifier;
            }
        }

        public string ApplicationName { get; set; }
        public Version ApplicationVersion { get; set; }
        public string ContactAddress { get; set; }

        public override string ToString()
        {
            return Generate(ApplicationName, ApplicationVersion, ContactAddress) ?? string.Empty;
        }
    }

    /// <summary>
    /// A page of results from the basecamp API, where the <see cref="NextUri"/> can be used to get the next page.
    /// </summary>
    /// <typeparam name="T">the type of elements in this collection</typeparam>
    public interface IPageCollection<T> : IReadOnlyCollection<T>
    {
        /// <summary>
        /// The URI to the next page of results.
        /// </summary>
        Uri NextUri { get; }

        /// <summary>
        /// Returns the next page of results (if any).
        /// </summary>
        /// <returns>the next page of results</returns>
        Task<IPageCollection<T>> ReadNextAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Returns an <see cref="IEnumerable{T}"/> that enumerates over this page and all remaining pages from the data source.
        /// </summary>
        IAsyncEnumerable<T> AsReadToEndEnumerable();

        /// <summary>
        /// Returns an <see cref="IEnumerator{T}"/> over this page and all remaining pages from the data source.
        /// </summary>
        IAsyncEnumerator<T> GetReadToEndEnumerator();
    }

    /// <summary>
    /// An <see cref="IEnumerable{T}"/> that can enumerate asynchronously.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IAsyncEnumerable<out T> : IEnumerable<T>
    {
        /// <summary>
        ///  Returns an enumerator that iterates through the collection asynchronously.
        /// </summary>
        /// <returns>A <see cref="IAsyncEnumerable{T}"/> that can be used to iterate through the collection</returns>
        IAsyncEnumerator<T> GetAsyncEnumerator();
    }

    /// <summary>
    /// An <see cref="IEnumerator{T}"/> that can be enumerate asynchronously.
    /// </summary>
    /// <typeparam name="T">the type of element in the enumeration</typeparam>
    public interface IAsyncEnumerator<out T> : IEnumerator<T>
    {
        /// <summary>
        /// Advances the enumerator to the next element of the enumerator asynchronously.
        /// </summary>
        /// <param name="cancellationToken">the token to monitor for cancellation requests</param>
        /// <returns>true if the enumerator was successfully advanced to the next element, or false if the enumerator has passed the end of the collection</returns>
        Task<bool> MoveNextAsync(CancellationToken cancellationToken);
    }

    public class TokenResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("expires_in")]
        public long ExpiresIn { get; set; }

        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }
    }

    public class BucketResponse
    {
        [JsonProperty("id")]
        public long? Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }

    public class ProjectResponse
    {
        [JsonProperty("id")]
        public long? Id { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAtString { get; set; }

        [JsonIgnore]
        public DateTimeOffset? CreatedAt
        {
            get { return DateTimeOffsetConvert.Deserialize(CreatedAtString); }
            set { CreatedAtString = DateTimeOffsetConvert.Serialize(value); }
        }

        [JsonProperty("updated_at")]
        public string UpdatedAtString { get; set; }

        [JsonIgnore]
        public DateTimeOffset? UpdatedAt
        {
            get { return DateTimeOffsetConvert.Deserialize(UpdatedAtString); }
            set { UpdatedAtString = DateTimeOffsetConvert.Serialize(value); }
        }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("bookmark_url")]
        public string BookmarkUrl { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("app_url")]
        public string AppUrl { get; set; }

        [JsonProperty("dock")]
        public IReadOnlyCollection<DockResponse> Docks { get; set; }
    }

    public class DockResponse
    {
        [JsonProperty("id")]
        public long? Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("app_url")]
        public string AppUrl { get; set; }

        [JsonProperty("enabled")]
        public bool? Enabled { get; set; }
    }

    public class RecordingResponse
    {
        [JsonProperty("id")]
        public long? Id { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAtString { get; set; }

        [JsonIgnore]
        public DateTimeOffset? CreatedAt
        {
            get { return DateTimeOffsetConvert.Deserialize(CreatedAtString); }
            set { CreatedAtString = DateTimeOffsetConvert.Serialize(value); }
        }

        [JsonProperty("updated_at")]
        public string UpdatedAtString { get; set; }

        [JsonIgnore]
        public DateTimeOffset? UpdatedAt
        {
            get { return DateTimeOffsetConvert.Deserialize(UpdatedAtString); }
            set { UpdatedAtString = DateTimeOffsetConvert.Serialize(value); }
        }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("app_url")]
        public string AppUrl { get; set; }

        [JsonProperty("comments_count")]
        public int? CommentsCount { get; set; }

        [JsonProperty("comments_url")]
        public string CommentsUrl { get; set; }

        [JsonProperty("parent")]
        public ResponseParentResponse Parent { get; set; }

        [JsonProperty("bucket")]
        public BucketResponse Bucket { get; set; }

        [JsonProperty("creator")]
        public UserResponse Creator { get; set; }

        #region Event?

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("bookmark_url")]
        public string BookmarkUrl { get; set; }

        [JsonProperty("subscription_url")]
        public string SubscriptionUrl { get; set; }

        [JsonProperty("summary")]
        public string Summary { get; set; }

        [JsonProperty("all_day")]
        public bool? AllDay { get; set; }

        [JsonProperty("starts_at")]
        public string StartsAtString { get; set; }

        [JsonIgnore]
        public DateTimeOffset? StartsAt
        {
            get { return DateTimeOffsetConvert.Deserialize(StartsAtString); }
            set { StartsAtString = DateTimeOffsetConvert.Serialize(value); }
        }

        [JsonProperty("ends_at")]
        public string EndsAtString { get; set; }

        [JsonIgnore]
        public DateTimeOffset? EndsAt
        {
            get { return DateTimeOffsetConvert.Deserialize(EndsAtString); }
            set { EndsAtString = DateTimeOffsetConvert.Serialize(value); }
        }

        [JsonProperty("participants")]
        public IReadOnlyCollection<UserResponse> Participants { get; set; }

        #endregion
    }

    public class ResponseParentResponse
    {
        [JsonProperty("id")]
        public long? Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("app_url")]
        public string AppUrl { get; set; }
    }

    public class EventResponse
    {
        [JsonProperty("id")]
        public long? Id { get; set; }

        [JsonProperty("recording_id")]
        public long? RecordingId { get; set; }

        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("details")]
        public EventDetails Details { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAtString { get; set; }

        [JsonIgnore]
        public DateTimeOffset? CreatedAt
        {
            get { return DateTimeOffsetConvert.Deserialize(CreatedAtString); }
            set { CreatedAtString = DateTimeOffsetConvert.Serialize(value); }
        }

        public UserResponse User { get; set; }
    }

    public class EventDetails
    {
        [JsonProperty("notified_recipient_ids")]
        public IReadOnlyCollection<int> NotifiedRecipientIds { get; set; }
    }

    public class UserResponse
    {
        [JsonProperty("id")]
        public long? Id { get; set; }

        [JsonProperty("attachable_sgid")]
        public string AttachableSgid { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("email_address")]
        public string EmailAddress { get; set; }

        [JsonProperty("personable_type")]
        public string PersonableType { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("bio")]
        public string Bio { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAtString { get; set; }

        [JsonIgnore]
        public DateTimeOffset? CreatedAt
        {
            get { return DateTimeOffsetConvert.Deserialize(CreatedAtString); }
            set { CreatedAtString = DateTimeOffsetConvert.Serialize(value); }
        }

        [JsonProperty("updated_at")]
        public string UpdatedAtString { get; set; }

        [JsonIgnore]
        public DateTimeOffset? UpdatedAt
        {
            get { return DateTimeOffsetConvert.Deserialize(UpdatedAtString); }
            set { UpdatedAtString = DateTimeOffsetConvert.Serialize(value); }
        }

        [JsonProperty("admin")]
        public bool? Admin { get; set; }

        [JsonProperty("owner")]
        public bool? Owner { get; set; }

        [JsonProperty("time_zone")]
        public string TimeZone { get; set; }

        [JsonProperty("avatar_url")]
        public string AvatarUrl { get; set; }

        public CompanyResponse Company { get; set; }
    }

    public class CompanyResponse
    {
        [JsonProperty("id")]
        public long? Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
