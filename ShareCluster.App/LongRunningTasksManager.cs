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
        private readonly object _syncLock = new object();
        private readonly ILogger<LongRunningTasksManager> _logger;
        private ImmutableDictionary<int, LongRunningTask> _tasks;

        public LongRunningTasksManager(ILoggerFactory loggerFactory)
        {
            _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<LongRunningTasksManager>();
            _tasks = ImmutableDictionary<int, LongRunningTask>.Empty;
        }

        public event EventHandler<DictionaryChangedEvent<int, LongRunningTask>> TasksChanged;

        public IImmutableDictionary<int, LongRunningTask> Tasks => _tasks;

        public void AddTaskToQueue(LongRunningTask task)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            _logger.LogDebug($"New task: {task.Title}");

            lock (_syncLock)
            {
                if (_tasks.ContainsKey(task.Id)) return;
                _tasks = _tasks.Add(task.Id, task);
                task.Task.ContinueWith(t => OnCompletedTask(task));

                // notify
                TasksChanged?.Invoke(this,DictionaryChangedEvent<int, LongRunningTask>
                    .FromNullableEnumerable(added: new[] { new KeyValuePair<int, LongRunningTask>(task.Id, task) }, removed: null, changed: null));
            }
        }
        
        private void OnCompletedTask(LongRunningTask task)
        {
            lock(_syncLock)
            {
                if (!task.Task.IsCompleted) throw new InvalidOperationException("Task is unfinished.");

                // check result and add to completed
                if (task.Task.IsCompletedSuccessfully)
                {
                    _logger.LogInformation($"Task success. Duration {task.Elapsed}. Title: {task.Title}");
                }
                else
                {
                    _logger.LogError(task.Task.Exception, $"Task failed. Duration {task.Elapsed}. Title: {task.Title}. Reason: {task.Task.Exception.Message}");
                }
            }
        }

        public void CleanCompletedTasks()
        {
            lock(_syncLock)
            {
                var toRemove = _tasks.Where(t => t.Value.Task.IsCompleted).ToList();

                _tasks = _tasks.RemoveRange(toRemove.Select(t => t.Key));

                TasksChanged?.Invoke(this, DictionaryChangedEvent<int, LongRunningTask>
                    .FromNullableEnumerable(added: null, removed: toRemove, changed: null));
            }
        }
    }
}
