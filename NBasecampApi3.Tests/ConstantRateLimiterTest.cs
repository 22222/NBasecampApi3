using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBasecampApi3
{
    public class ConstantRateLimiterTest
    {
        [Test]
        public async Task Default_Wait51Times()
        {
            var rateLimiter = new ConstantRateLimiter();

            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < 51; i++)
            {
                await rateLimiter.WaitIfNecessaryAsync();
            }
            stopwatch.Stop();

            // It should take at least 10 seconds to do 50 requests (so the 51st request should happen after 10 seconds).
            Assert.GreaterOrEqual(stopwatch.Elapsed.TotalSeconds, 10d);
        }
    }
}
