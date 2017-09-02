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
        Stopwatch stopwatch;

        public LongRunningTask(string title, Task task, string successProgress = null)
        {
            if (string.IsNullOrEmpty(title)) throw new ArgumentException("message", nameof(title));
            if (task == null) throw new ArgumentNullException(nameof(task));

            Title = title;
            ProgressText = "Running";
            stopwatch = Stopwatch.StartNew();

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
                stopwatch.Stop();
                if (t.IsFaulted)
                {
                    // extract exception if single exception
                    var flattenExc = t.Exception.Flatten();
                    Exception exc = (flattenExc.InnerExceptions.Count == 1 ? flattenExc.InnerExceptions.First(): flattenExc);
                    UpdateProgress($"Error: {exc}");
                    FaultException = exc;
                }
                else
                {
                    UpdateProgress(successProgress ?? "Success");
                }

                IsCompleted = true;

                return this;
            });
        }

        public LongRunningTask UpdateProgress(string progressText)
        {
            ProgressText = progressText;
            return this;
        }

        public string Title { get; protected set; }

        public virtual string ProgressText { get; private set; }

        public Task<LongRunningTask> CompletionTask { get; private set; }

        public bool IsCompletedSuccessfully => IsCompleted && !IsFaulted;
        public bool IsRunning => !IsCompleted;
        public bool IsCompleted { get; private set; }
        public bool IsFaulted => FaultException != null;
        public Exception FaultException { get; private set; }
        public TimeSpan Elapsed => stopwatch.Elapsed;

        public string StatusText =>
            (IsCompletedSuccessfully) ? "Success" :
            (IsFaulted) ? "Error" :
            "Running";
    }
}