using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace ShareCluster
{
    public class MeasureItem
    {
        private int internalValue;
        private int internalCounter;
        private double lastResult;
        private readonly Stopwatch stopwatch;
        private readonly TimeSpan measureLimit;
        private readonly object syncLock = new object();

        public MeasureItem(MeasureType measureType)
        {
            measureLimit = TimeSpan.FromSeconds(5);
            stopwatch = Stopwatch.StartNew();
            MeasureType = measureType;
        }

        public MeasureType MeasureType { get; }

        public void Put(int value)
        {
            RecalculateIfNeeded();

            // data can be collected between these two commands, but thats OK
            // - it can only affect statistics a little
            Interlocked.Increment(ref internalCounter);
            Interlocked.Add(ref internalValue, value);
        }
        
        public string ValueFormatted
        {
            get
            {
                RecalculateIfNeeded();

                if (MeasureType == MeasureType.Throughput)
                {
                    return $"{SizeFormatter.ToString((long)lastResult)}/s";
                }
                
                if(MeasureType == MeasureType.CounterAverage)
                {
                    return $"{lastResult:0.0}/s";
                }

                return lastResult.ToString("0.0");
            }
        }

        private void RecalculateIfNeeded()
        {
            if (stopwatch.Elapsed < measureLimit) return;
            lock (syncLock)
            {
                if (stopwatch.Elapsed > measureLimit)
                {
                    // timing
                    TimeSpan elapsed = stopwatch.Elapsed;
                    stopwatch.Restart();

                    // collect
                    int internalCounterCollected = Interlocked.Exchange(ref internalCounter, 0);
                    double internalValueCollected = Interlocked.Exchange(ref internalValue, 0);

                    // calculate result
                    double newValue = ResolveValue(internalCounterCollected, internalValueCollected, elapsed, MeasureType);
                    Interlocked.Exchange(ref lastResult, newValue);
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
