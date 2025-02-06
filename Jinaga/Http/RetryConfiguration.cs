using System;

namespace Jinaga.Http
{
    public class RetryConfiguration
    {
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(5);
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
        public double BackoffMultiplier { get; set; } = 2.0;
        public bool Enabled { get; set; } = true;
    }
}
