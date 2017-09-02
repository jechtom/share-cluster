using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster
{
    /// <summary>
    /// Provides planning and execution of long running tasks like extracting packages, deleting packages, creating packages, waiting for package to discover etc.
    /// Remark: Downloading of packages is managed by <see cref="Network.PackageDownloadManager"/>.
    /// </summary>
    public class LongRunningTasksManager
    {
        private readonly object syncLock = new object();
        private readonly ILogger<LongRunningTasksManager> logger;
        private ImmutableList<LongRunningTask> tasks;
        private ImmutableList<LongRunningTask> completedTasks;

        public LongRunningTasksManager(ILoggerFactory loggerFactory)
        {
            logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<LongRunningTasksManager>();
            tasks = ImmutableList<LongRunningTask>.Empty;
            completedTasks = ImmutableList<LongRunningTask>.Empty;
        }

        public IImmutableList<LongRunningTask> Tasks => tasks;
        public IImmutableList<LongRunningTask> CompletedTasks => completedTasks;

        public void AddTaskToQueue(LongRunningTask task)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            logger.LogDebug($"Added task \"{task.Title}\" to queue.");

            lock (syncLock)
            {
                if (tasks.Contains(task)) return;
                tasks = tasks.Add(task);
                task.CompletionTask.ContinueWith(t => OnCompletedTask(t.Result));
            }
        }
        
        private void OnCompletedTask(LongRunningTask task)
        {
            lock(syncLock)
            {
                // remove completed task
                tasks = tasks.Remove(task);

                if (!task.IsCompleted) throw new InvalidOperationException("Task is unfinished.");

                // check result and add to completed
                if (task.IsCompletedSuccessfully)
                {
                    logger.LogInformation($"Task \"{task.Title}\" completed successfully. Has been running for {task.Elapsed}.");
                }
                else
                {
                    logger.LogError(task.FaultException, $"Task \"{task.Title}\" (has been running for {task.Elapsed}): {task.ProgressText ?? "Unknown reason"}");
                }
                completedTasks = completedTasks.Insert(index: 0, item: task);
            }
        }

        public void CleanCompletedTasks()
        {
            lock(syncLock)
            {
                completedTasks = ImmutableList<LongRunningTask>.Empty;
            }
        }
    }
}
