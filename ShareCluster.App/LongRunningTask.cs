using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ShareCluster
{
    /// <summary>
    /// Long running task representation.
    /// </summary>
    public class LongRunningTask
    {
        static int _counter = 0;

        Stopwatch _stopwatch;
        
        public LongRunningTask(string title, Task task)
        {
            if (string.IsNullOrEmpty(title)) throw new ArgumentException(nameof(title));
            Title = title;
            Id = Interlocked.Increment(ref _counter);
            Task = task ?? throw new ArgumentNullException(nameof(task));
            _stopwatch = Stopwatch.StartNew();

            try
            {
                if (task.Status == TaskStatus.Created) task.Start();
            }
            catch (Exception e)
            {
                task = Task.FromException(new Exception($"Can't start task: {e.Message}", e));
            }

            task.ContinueWith(t =>
            {
                _stopwatch.Stop();
                return this;
            });
        }

        public int Id { get; }

        public Task Task { get; }

        public string Title { get; private set; }

        public TimeSpan Elapsed => _stopwatch.Elapsed;

        public MeasureItem Measure { get; private set; }

        public override int GetHashCode() => Id;

        public override bool Equals(object obj) => ((LongRunningTask)obj).Id == Id;

        public LongRunningTask WithMeasure(MeasureItem measure)
        {
            Measure = measure;
            return this;
        }
    }
}
