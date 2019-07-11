using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace ShareCluster.Synchronization
{
    /// <summary>
    /// Provides scheduling for refresh tasks that should not happen too often.
    /// </summary>
    public class ThrottlingTimer
    {
        private readonly ThrottlingTimerCallback _callback;
        private readonly Timer _timer;
        private readonly Stopwatch _time;
        private readonly object _syncLock = new object();
        private TimeSpan? _scheduledNextTime;
        private TimeSpan? _lastFinishTime;
        private bool _isRunning;
        private bool _isStopped;
        private int _nextExecutionIndex;

        /// <summary>
        /// Creates new instance of <see cref="ThrottlingTimer"/>
        /// </summary>
        /// <param name="minimumDelayBetweenExecutions">Sets minimum delay between end of last run and start of next scheduled run.</param>
        /// <param name="scheduleDelay">Sets delay to wait after scheduling to possibly group multiple scheduling requests at short time (for example some update batches etc.)</param>
        public ThrottlingTimer(TimeSpan minimumDelayBetweenExecutions, TimeSpan scheduleDelay, ThrottlingTimerCallback callback)
        {
            MinimumDelayBetweenExecutions = minimumDelayBetweenExecutions;
            ScheduleDelay = scheduleDelay;
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            _timer = new Timer(OnTimerCallback, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _time = Stopwatch.StartNew();
        }

        private void OnTimerCallback(object state)
        {
            int currentExecutionIndex;
            lock (_syncLock)
            {
                if (_isStopped) return;
                _scheduledNextTime = null;
                currentExecutionIndex = _nextExecutionIndex++;
                _isRunning = true;
            }

            try
            {
                _callback.Invoke(new ThrottlingTimerContext(currentExecutionIndex));
            }
            catch
            {
                throw;
            }

            lock(_syncLock)
            {
                _isRunning = false;
                _lastFinishTime = _time.Elapsed;
                if (_isStopped) return;
                if (_scheduledNextTime != null) ScheduleNextTimerTick(); // schedule next tick as next run has been requested
            }
        }

        public TimeSpan MinimumDelayBetweenExecutions { get; }
        public TimeSpan ScheduleDelay { get; }

        public void Schedule()
        {
            lock(_syncLock)
            {
                if (_isStopped) return; // stopped - closed
                if (_scheduledNextTime != null) return; // already scheduled
                _scheduledNextTime = _time.Elapsed;
                if (_isRunning) return; // already running - after finish, next run will be planned
                ScheduleNextTimerTick();
            }
        }

        private void ScheduleNextTimerTick()
        {
            // calculate delay until next run based on scheduled time and schedule delay
            TimeSpan untilNextRun = _scheduledNextTime.Value + ScheduleDelay - _time.Elapsed;

            if (_lastFinishTime != null)
            {
                // delay execution with minimum delay value
                TimeSpan closestNextRunByMinimumDelay = _lastFinishTime.Value + MinimumDelayBetweenExecutions - _time.Elapsed;
                if (closestNextRunByMinimumDelay > untilNextRun) untilNextRun = closestNextRunByMinimumDelay;
            }

            // run now?
            if (untilNextRun < TimeSpan.Zero) untilNextRun = TimeSpan.Zero;

            // schedule tick
            _timer.Change(untilNextRun, Timeout.InfiniteTimeSpan);
        }

        public void Stop()
        {
            lock (_syncLock)
            {
                _isStopped = true;
            }
        }
    }

    public delegate void ThrottlingTimerCallback(ThrottlingTimerContext context);
}
