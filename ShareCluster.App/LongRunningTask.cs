using System;
using System.Diagnostics;
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
            if (string.IsNullOrEmpty(title))
            {
                throw new ArgumentException("message", nameof(title));
            }

            Title = title;
            Task = task ?? throw new ArgumentNullException(nameof(task));
            ProgressText = "Running";

            stopwatch = Stopwatch.StartNew();

            try
            {
                if (task.Status == TaskStatus.Created) task.Start();
            }
            catch (Exception e)
            {
                Task = Task.FromException(new Exception($"Can't start task: {e.Message}", e));
            }

            Task = Task.ContinueWith(t =>
            {
                stopwatch.Stop();
                if (t.IsFaulted)
                {
                    Exception exc = t.Exception.InnerException;
                    UpdateProgress($"Error: {exc}");
                    throw exc;
                }
                else
                {
                    UpdateProgress(successProgress ?? "Success");
                }
            });
        }
        
        public LongRunningTask UpdateProgress(string progressText)
        {
            ProgressText = progressText;
            return this;
        }

        public string Title { get; protected set; }

        public virtual string ProgressText { get; private set; }

        public Task Task { get; private set; }

        public bool IsCompletedSuccessfully => Task.IsCompletedSuccessfully;
        public bool IsRunning => !Task.IsCompleted;
        public bool IsCompleted => Task.IsCompleted;
        public bool IsFaulted => Task.IsFaulted;

        public TimeSpan Elapsed => stopwatch.Elapsed;

        public string StatusText =>
            (IsCompletedSuccessfully) ? "Success" :
            (IsFaulted) ? "Error" :
            "Running";
    }
}