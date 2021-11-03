using System;

namespace RateLimiter
{
    public class ThrottleInfo
    {
        public ThrottleInfo(TimeSpan throttledForTimeSpan)
        {
            ThrottledForTimeSpan = throttledForTimeSpan;
        }

        public TimeSpan ThrottledForTimeSpan { get; }
    }
}