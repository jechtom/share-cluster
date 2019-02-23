using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShareCluster.Synchronization
{
    /// <summary>
    /// Task scheduler with limit for number of concurrently running task in parallel and queue for waiting tasks.
    /// </summary>
    public class TaskSemaphoreQueue
    {
        private readonly Queue<Item> _queue = new Queue<Item>();
        private readonly List<TaskCompletionSource<object>> _notifyAfterCleanQueueAndCompleted;
        private readonly object _syncLock = new object();
        private int _runningTasks;
        
        public TaskSemaphoreQueue(int runningTasksLimit)
        {
            if (runningTasksLimit <= 0) throw new ArgumentException(nameof(runningTasksLimit));
            RunningTasksLimit = runningTasksLimit;
            _notifyAfterCleanQueueAndCompleted = new List<TaskCompletionSource<object>>();
        }

        public int RunningTasksLimit { get; }

        /// <summary>
        /// Enqueues given task.
        /// </summary>
        /// <param name="args">Args use to create task</param>
        /// <param name="createTaskCall">Task factory</param>
        public void EnqueueTaskFactory<TArgs>(TArgs args, Func<TArgs, Task> createTaskCall)
        {
            lock(_syncLock)
            {
                _queue.Enqueue(new Item(args, (arg) => createTaskCall((TArgs)arg)));
                TryRunningTask();
            }
        }

        private void TryRunningTask()
        {
            lock(_syncLock)
            {
                while (_queue.Count > 0)
                {
                    // all slots used?
                    if (_runningTasks >= RunningTasksLimit) return;
                    Interlocked.Increment(ref _runningTasks);

                    // next to process
                    Item next = _queue.Dequeue();
                    next.CreateTaskCall(next.Args)
                        .ContinueWith((task) => AfterTaskFinished(next, task));
                }
            }
        }

        public async Task WaitForFinishAllTasksAsync()
        {
            var tcs = new TaskCompletionSource<object>();

            lock (_syncLock)
            {
                _notifyAfterCleanQueueAndCompleted.Add(tcs);
                NotifyAfterCleanQueueAndCompleted();
            }

            await tcs.Task;
        }

        private void AfterTaskFinished(Item item, Task task)
        {
            lock (_syncLock)
            {
                Interlocked.Decrement(ref _runningTasks);

                // try schedule next
                TryRunningTask();

                NotifyAfterCleanQueueAndCompleted();
            }
        }

        private void NotifyAfterCleanQueueAndCompleted()
        {
            if (_notifyAfterCleanQueueAndCompleted.Any() && _runningTasks == 0)
            {
                _notifyAfterCleanQueueAndCompleted.ForEach(t => t.TrySetResult(null));
                _notifyAfterCleanQueueAndCompleted.Clear();
            }
        }

        /// <summary>
        /// Removes all waiting items from queue that has not been started yet.
        /// </summary>
        public void ClearQueued()
        {
            lock (_syncLock)
            {
                _queue.Clear();
            }
        }
        
        class Item
        {
            public Item(object args, Func<object, Task> createTaskCall)
            {
                Args = args;
                CreateTaskCall = createTaskCall ?? throw new ArgumentNullException(nameof(createTaskCall));
            }

            public object Args { get; }
            public Func<object, Task> CreateTaskCall { get; }
        }
    }
}
