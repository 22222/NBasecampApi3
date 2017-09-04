This is a start to a .NET client for version 3 of the Basecamp API.  It only implements a few of the endpoints so far, and it isn't very well tested.  It also has a stupid name.

Installation
============
There's no NuGet package for this library so far.  This is only a very limited start to a library, and you'll probably have to make changes to add endpoints you need.  So the recommended way to use this is to just copy the source code into your own project.

If this library is ever finished, then a NuGet package will be created.  But that might never happen, who knows.


Getting Started
===============
The main class is `BasecampClient`.  Here's a basic example of using it to get a list of projects:

```c#
using NBasecampApi3;

int accountId = 999999999;
string accessToken = "YOUR_OAUTH_TOKEN";

var basecampClient = new BasecampClient(accountId, accessToken);
var projects = await basecampClient.GetProjectsAsync();
foreach (var project in projects)
{
    Console.WriteLine($"Project {project.Name} ({project.Id})");
}
```

That example used an access token directly.  But an access token expires, so you'll probably want a client that automatically aquire access tokens as needed.  For that, you can use an `AccessTokenSource`:

```c#
int accountId = 999999999;
string clientId = "YOUR_CLIENT_ID";
string clientSecret = "YOUR_CLIENT_SECRET";
string redirectUrl = "YOUR_REDIRECT_URL";
string refreshToken = "YOUR_REFRESH_TOKEN";

var accessTokenSource = new AccessTokenSource(
    clientId: clientId, 
    clientSecret: clientSecret,
    redirectUrl: redirectUrl,
    refreshToken: refreshToken
);
var basecampClient = new BasecampClient(accountId, accessTokenSource);
```

Most of the endpoints that return a list of items can be split up into pages.  In the example above, that list of projects only gives you the first page:

```c#
var projects = await basecampClient.GetProjectsAsync();
foreach (var project in projects)
{
    Console.WriteLine($"Project {project.Name} ({project.Id})");
}
```

The return value from these methods is a custom `IPageCollection<T>` interface that includes a `NextUri` property.  It also includes the `GetReadToEndEnumerator()` and `AsReadToEndEnumerable()` helper methods for reading from all of the pages:

```c#
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
```

Advanced Options
================
There are several more options you can use with `BasecampClient` through the `BasecampClientOptions` class.

User Agent
----------
Once you're actually using this API for something, you'll probably want to include a user agent for your own application:

```c#
var basecampClientOptions = new BasecampClientOptions(accountId, accessTokenSource)
{
    UserAgent = UserAgent.Generate("YOUR_APPLICATION", "YOUR_CONTACT_ADDRESS"),
};
var basecampClient = new BasecampClient(basecampClientOptions);
```

Rate Limiter
------------
The basecamp API has a rate limit of "50 requests per 10-second period".  The easiest way to meet that requirement is with a constant delay, so by default the client enforces a 200 ms delay per requests.

That's controlled by the `IRateLimiter` interface.  So if you want you want to change that behavior, you can pass in your own rate limiter:

```c#
var basecampClientOptions = new BasecampClientOptions(accountId, accessTokenSource)
{
    RateLimiter = new ConstantRateLimiter(delayMs: 500),
};
var basecampClient = new BasecampClient(basecampClientOptions);
```

The client will also retry once if it receives a `429` response, so you could choose to use the client with no rate limiter of your own if you don't expect to [push it to the limit](https://www.youtube.com/watch?v=L-6ugLM3ARw) very often.


Response Cache
--------------
The client does not include any response caching by default, but it does provide a `ResponseMessageCache` option you can pass in.

The best way to implement that would probably be through a caching library, like `System.Runtime.Caching.MemoryCache` or [ASP.NET Caching](https://github.com/aspnet/Caching).  But as a proof of concept, this library does include a `SingleResponseMessageCache` that only caches one response at a time:

```c#
var basecampClientOptions = new BasecampClientOptions(accountId, accessTokenSource)
{
    ResponseMessageCache = new SingleResponseMessageCache(),
};
var basecampClient = new BasecampClient(basecampClientOptions);
```


Exceptions
==========
If any errors are encountered when trying to connect to the Basecamp API, a `BasecampException` will be thrown.

* `BasecampException` - the base class for all exceptions from the Basecamp API
* `BasecampResponseException` - for an error in the response to the API, such as a 4xx or 5xx HTTP response
	* Message will be parsed from the `error` property of the JSON response (if available)
	* Has properties to get the HTTP status code and the full HTTP content from the response
* `BasecampUnauthorizedResponseException` - for an HTTP 401 error, such as expired or invalid credentials
* `BasecampTooManyRequestsException` - for an HTTP 429 error, when you've exceeded the rate limit for the Basecamp API
	* The client will automatically retry once on a 429 error, but a second error on the same request will cause this exception to be thrown