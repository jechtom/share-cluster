using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ShareCluster.Network
{
    public class PostponeTimer
    {

        static Stopwatch _stopwatch = Stopwatch.StartNew();

        public static PostponeTimer NoPostpone { get; } = new PostponeTimer();

        private TimeSpan? _postponedUntil;

        public PostponeTimer()
        {
            _postponedUntil = null;
        }

        public PostponeTimer(TimeSpan interval)
        {
            _postponedUntil = _stopwatch.Elapsed.Add(interval);
        }

        public bool IsPostponed
        {
            get
            {
                if (_postponedUntil == null) return false;
                if (_postponedUntil.Value > _stopwatch.Elapsed) return true;
                _postponedUntil = null;
                return false;
            }
        }
    }
}
