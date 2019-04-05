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
        Func<LongRunningTask, string> _progressFunc;
        
        public LongRunningTask(string title, Task task, string successProgress = null, Func<LongRunningTask, string> progressFunc = null)
        {
            if (string.IsNullOrEmpty(title)) throw new ArgumentException(nameof(title));
            Title = title;
            Id = Interlocked.Increment(ref _counter);
            Task = task ?? throw new ArgumentNullException(nameof(task));
            _progressFunc = progressFunc ?? ((t) => "Running");
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

        public string Title { get; protected set; }

        public virtual string ProgressText => _progressFunc(this);

        public TimeSpan Elapsed => _stopwatch.Elapsed;

        public override int GetHashCode() => Id;

        public override bool Equals(object obj) => ((LongRunningTask)obj).Id == Id;
    }
}
