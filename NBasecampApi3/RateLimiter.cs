using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBasecampApi3
{
    /// <summary>
    /// Applies a rate limit to API requests.
    /// </summary>
    public interface IRateLimiter
    {
        /// <summary>
        /// Wait for the next available time to make an API request.
        /// </summary>
        Task WaitIfNecessaryAsync();
    }

    /// <summary>
    /// Limits requests by a constant rate.
    /// </summary>
    public class ConstantRateLimiter : IRateLimiter
    {
        /// <summary>
        /// A default rate limiter for the basecamp API.
        /// </summary>
        public static ConstantRateLimiter Default { get; } = new ConstantRateLimiter();

        private readonly int delayMs;
        private Stopwatch stopwatch;

        /// <summary>
        /// Constructs a rate limiter for 50 requests every 10 seconds (a 200 millisecond delay per request).
        /// </summary>
        public ConstantRateLimiter() : this(200) { }

        /// <summary>
        /// Constructs a rate limiter with the specified delay per request.
        /// </summary>
        /// <param name="delayMs">the millisecond delay between requests to the basecamp API</param>
        public ConstantRateLimiter(int delayMs)
        {
            this.delayMs = delayMs;
            this.stopwatch = Stopwatch.StartNew();
        }

        /// <inheritdoc />
        public async Task WaitIfNecessaryAsync()
        {
            var nextDelayMs = delayMs - stopwatch.ElapsedMilliseconds;
            if (nextDelayMs > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(nextDelayMs));
            }
            stopwatch.Restart();
        }
    }
}
