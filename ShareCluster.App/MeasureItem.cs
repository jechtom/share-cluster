using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace ShareCluster
{
    public class MeasureItem
    {
        private int _internalValue;
        private int _internalCounter;
        private double _lastResult;
        private readonly Stopwatch _stopwatch;
        private readonly TimeSpan _measureLimit;
        private readonly object _syncLock = new object();

        public MeasureItem(MeasureType measureType)
        {
            _measureLimit = TimeSpan.FromSeconds(2);
            _stopwatch = Stopwatch.StartNew();
            MeasureType = measureType;
        }

        public MeasureType MeasureType { get; }

        public void Put(int value)
        {
            RecalculateIfNeeded();

            // data can be collected between these two commands, but thats OK
            // - it can only affect statistics a little
            Interlocked.Increment(ref _internalCounter);
            Interlocked.Add(ref _internalValue, value);
        }
        
        public string ValueFormatted
        {
            get
            {
                RecalculateIfNeeded();

                if (MeasureType == MeasureType.Throughput)
                {
                    return $"{SizeFormatter.ToString((long)_lastResult)}/s";
                }
                
                if(MeasureType == MeasureType.CounterAverage)
                {
                    return $"{_lastResult:0.0}/s";
                }

                return _lastResult.ToString("0.0");
            }
        }

        private void RecalculateIfNeeded()
        {
            if (_stopwatch.Elapsed < _measureLimit) return;
            lock (_syncLock)
            {
                if (_stopwatch.Elapsed > _measureLimit)
                {
                    // timing
                    TimeSpan elapsed = _stopwatch.Elapsed;
                    _stopwatch.Restart();

                    // collect
                    int internalCounterCollected = Interlocked.Exchange(ref _internalCounter, 0);
                    double internalValueCollected = Interlocked.Exchange(ref _internalValue, 0);

                    // calculate result
                    double newValue = ResolveValue(internalCounterCollected, internalValueCollected, elapsed, MeasureType);
                    Interlocked.Exchange(ref _lastResult, newValue);
                }
            }
        }

        private static double ResolveValue(double counter, double value, TimeSpan elapsed, MeasureType type)
        {
            switch (type)
            {
                case MeasureType.TimeAverage:
                case MeasureType.Throughput:
                    return value / elapsed.TotalSeconds;
                case MeasureType.CounterAverage:
                    return counter == 0 ? 0 : (value / counter);
                case MeasureType.CounterTotal:
                default:
                    return value;
            }
        }
    }
}
