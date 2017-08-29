using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ShareCluster.Network
{
    public class PostponeTimer
    {

        static Stopwatch stopwatch = Stopwatch.StartNew();

        public static PostponeTimer NoPostpone { get; } = new PostponeTimer();

        private TimeSpan? postponedUntil;

        public PostponeTimer()
        {
            postponedUntil = null;
        }

        public PostponeTimer(TimeSpan interval)
        {
            postponedUntil = stopwatch.Elapsed.Add(interval);
        }

        public bool IsPostponed
        {
            get
            {
                if (postponedUntil == null) return false;
                if (postponedUntil.Value > stopwatch.Elapsed) return true;
                postponedUntil = null;
                return false;
            }
        }
    }
}
