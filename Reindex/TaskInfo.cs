using System;

namespace Reindex
{
    public class TaskInfo
    {
        public string Host { get; set; }

        public string Description { get; set; }

        public DateTime StartedAt { get; set; }

        public int Total { get; set; }

        public int Done { get; set; }

        public TimeSpan TimeRemaining()
        {
            var elapsed = DateTime.Now - StartedAt;
            if (elapsed.TotalMilliseconds == 0 || Done == 0) return TimeSpan.Zero;

            var apms = (double)Done / elapsed.TotalMilliseconds;
            var remainMs = (double)(Total - Done) / apms;

            return TimeSpan.FromMilliseconds(remainMs);
        }
    }
}
