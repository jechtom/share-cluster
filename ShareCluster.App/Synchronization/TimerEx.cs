using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShareCluster.Synchronization
{
    public class TimerEx
    {
        private Timer _timer;
        private TimeSpan _baseInterval;

        public delegate Task TimerExCallbackAsync(TimerExContext context);

        public TimerEx(TimerExCallbackAsync callBack)
        {
            _timer = new Timer(callback: TimerCallback);
        }

        private void TimerCallback(object _)
        {
            
        }

        public TimerEx WithAutomaticTriggerEvery(TimeSpan interval)
        {
            throw new NotImplementedException();
        }

        public TimerEx DoNotRunSoonerThan(TimeSpan minimumInterval)
        {
            throw new NotImplementedException();
        }

        public void Schedule()
        {
            throw new NotImplementedException();
        }

        public void KeepOffFor()
        {
            throw new NotImplementedException();
        }
    }
}
