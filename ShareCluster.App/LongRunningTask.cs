using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ShareCluster
{
    /// <summary>
    /// Long running task representation.
    /// </summary>
    public class LongRunningTask
    {
        Stopwatch _stopwatch;
        Func<LongRunningTask, string> _progressFunc;

        public LongRunningTask(string title, Task task, string successProgress = null, Func<LongRunningTask, string> progressFunc = null)
        {
            if (string.IsNullOrEmpty(title)) throw new ArgumentException("message", nameof(title));
            if (task == null) throw new ArgumentNullException(nameof(task));

            Title = title;
            this._progressFunc = progressFunc ?? ((t) => "Running");
            _stopwatch = Stopwatch.StartNew();

            try
            {
                if (task.Status == TaskStatus.Created) task.Start();
            }
            catch (Exception e)
            {
                task = Task.FromException(new Exception($"Can't start task: {e.Message}", e));
            }

            CompletionTask = task.ContinueWith(t =>
            {
                _stopwatch.Stop();
                if (t.IsFaulted)
                {
                    // extract exception if single exception
                    AggregateException flattenExc = t.Exception.Flatten();
                    Exception exc = (flattenExc.InnerExceptions.Count == 1 ? flattenExc.InnerExceptions.First(): flattenExc);
                    this._progressFunc = ((_) => $"Error: {exc}");
                    FaultException = exc;
                }
                else
                {
                    this._progressFunc = ((_) => $"{successProgress ?? "Success"}");
                }

                IsCompleted = true;

                return this;
            });
        }

        public string Title { get; protected set; }

        public virtual string ProgressText => _progressFunc(this);

        public Task<LongRunningTask> CompletionTask { get; private set; }

        public bool IsCompletedSuccessfully => IsCompleted && !IsFaulted;
        public bool IsRunning => !IsCompleted;
        public bool IsCompleted { get; private set; }
        public bool IsFaulted => FaultException != null;
        public Exception FaultException { get; private set; }
        public TimeSpan Elapsed => _stopwatch.Elapsed;

        public string StatusText =>
            (IsCompletedSuccessfully) ? "Success" :
            (IsFaulted) ? "Error" :
            "Running";
    }
}
