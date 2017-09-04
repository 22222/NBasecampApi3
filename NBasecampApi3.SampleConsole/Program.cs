using NBasecampApi3.Launchpad;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NBasecampApi3.SampleConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            var appConfig = new AppConfig();
            appConfig.Load();

            await RunAuthenticateCookieAsync(appConfig);
            await RunAuthenticateVerificationCodeAsync(appConfig);

            await RunSample1Async(appConfig);
            await RunSample2Async(appConfig);
            await RunSample3Async(appConfig);
        }

        static async Task RunAuthenticateCookieAsync(AppConfig appConfig)
        {
            string clientId = appConfig.ClientId;
            string clientSecret = appConfig.ClientSecret;
            string redirectUrl = appConfig.RedirectUrl;
            string cookie = appConfig.BasecampCookie;
            string username = appConfig.Username;

            if (cookie == null)
            {
                Console.WriteLine("No basecamp cookie");
                return;
            }

            var oauthOptions = new OAuthOptions(clientId, clientSecret, redirectUrl);
            var client = new LaunchpadClient(oauthOptions);
            var accessTokenSource = await client.AuthenticateCookieAsync(cookie, username);

            var refreshToken = accessTokenSource.RefreshToken;
            Console.WriteLine($"Refresh token = {refreshToken}");

            var accessToken = await accessTokenSource.GetAccessTokenAsync();
            Console.WriteLine($"Access token = {accessToken}");
        }

        static async Task RunAuthenticateVerificationCodeAsync(AppConfig appConfig)
        {
            string clientId = appConfig.ClientId;
            string clientSecret = appConfig.ClientSecret;
            string redirectUrl = appConfig.RedirectUrl;
            string verificationCode = appConfig.VerificationCode;

            if (verificationCode == null)
            {
                Console.WriteLine("No verification code");
                return;
            }

            var oauthOptions = new OAuthOptions(clientId, clientSecret, redirectUrl);
            var client = new LaunchpadClient(oauthOptions);
            var accessTokenSource = await client.AuthenticateVerificationCodeAsync(verificationCode);

            var refreshToken = accessTokenSource.RefreshToken;
            Console.WriteLine($"Refresh token = {refreshToken}");

            var accessToken = await accessTokenSource.GetAccessTokenAsync();
            Console.WriteLine($"Access token = {accessToken}");
        }

        static async Task RunSample1Async(AppConfig appConfig)
        {
            int accountId = 999999999;
            string accessToken = "YOUR_OAUTH_TOKEN";

            accountId = appConfig.AccountId;
            accessToken = appConfig.AccessToken;
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                Console.WriteLine("No access token");
                return;
            }

            var basecampClient = new BasecampClient(accountId, accessToken);
            var projects = await basecampClient.GetProjectsAsync();
            foreach (var project in projects)
            {
                Console.WriteLine($"Project {project.Name} ({project.Id})");
            }

            if (projects.NextUri != null)
            {
                var moreProjects = await basecampClient.GetMoreProjectsAsync(projects.NextUri);
                foreach (var project in moreProjects)
                {
                    Console.WriteLine($"Project {project.Name} ({project.Id})");
                }
            }

        }

        static async Task RunSample2Async(AppConfig appConfig)
        {
            int accountId = 999999999;
            string clientId = "YOUR_CLIENT_ID";
            string clientSecret = "YOUR_CLIENT_SECRET";
            string redirectUrl = "YOUR_REDIRECT_URL";
            string refreshToken = "YOUR_REFRESH_TOKEN";

            accountId = appConfig.AccountId;
            clientId = appConfig.ClientId;
            clientSecret = appConfig.ClientSecret;
            redirectUrl = appConfig.RedirectUrl;
            refreshToken = appConfig.RefreshToken;

            var accessTokenSource = new AccessTokenSource(
                clientId: clientId,
                clientSecret: clientSecret,
                redirectUrl: redirectUrl,
                refreshToken: refreshToken
            );
            var basecampClient = new BasecampClient(accountId, accessTokenSource);


            var projects = await basecampClient.GetProjectsAsync();
            foreach (var project in projects.AsReadToEndEnumerable())
            {
                Console.WriteLine($"Project {project.Name} ({project.Id})");
            }
       
            var projectEnumerator = projects.GetReadToEndEnumerator();
            while (await projectEnumerator.MoveNextAsync(CancellationToken.None))
            {
                var project = projectEnumerator.Current;
                Console.WriteLine($"Project {project.Name} ({project.Id})");
            }

            var recordings = await basecampClient.GetRecordingsAsync(RecordingTypeEnum.ScheduleEntry);
            var bucketId = recordings.First().Bucket.Id.Value;
            var recordingId = recordings.First().Id.Value;
            var events = await basecampClient.GetEventsAsync(bucketId, recordingId);
            Console.WriteLine();
            Console.WriteLine("First Page Events:");

            foreach (var e in events)
            {
                Console.WriteLine($"Event {e.Action} ({e.Id})");
            }
        }

        static async Task RunSample3Async(AppConfig appConfig)
        {
            int accountId = 999999999;
            string clientId = "YOUR_CLIENT_ID";
            string clientSecret = "YOUR_CLIENT_SECRET";
            string redirectUrl = "YOUR_REDIRECT_URL";
            string refreshToken = "YOUR_REFRESH_TOKEN";

            accountId = appConfig.AccountId;
            clientId = appConfig.ClientId;
            clientSecret = appConfig.ClientSecret;
            redirectUrl = appConfig.RedirectUrl;
            refreshToken = appConfig.RefreshToken;

            var accessTokenSource = new AccessTokenSource(
                clientId: clientId,
                clientSecret: clientSecret,
                redirectUrl: redirectUrl,
                refreshToken: refreshToken
            );
            var basecampClientOptions = new BasecampClientOptions(accountId, accessTokenSource)
            {
                UserAgent = UserAgent.Generate("YOUR_APPLICATION", "YOUR_CONTACT_ADDRESS"),
                RateLimiter = new ConstantRateLimiter(delayMs: 500),
                ResponseMessageCache = new SingleResponseMessageCache(),
            };
            var basecampClient = new BasecampClient(basecampClientOptions);
        }
    }
}
