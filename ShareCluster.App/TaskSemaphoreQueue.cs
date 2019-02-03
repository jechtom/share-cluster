using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster
{
    /// <summary>
    /// Task scheduler with limit for number of running task in parallel and deduplication by given <typeparamref name="TKey"/>.
    /// </summary>
    public class TaskSemaphoreQueue<TKey, TData>
    {
        private readonly Dictionary<TKey, Item> _all = new Dictionary<TKey, Item>();
        private readonly Queue<TKey> _waiting = new Queue<TKey>();
        private readonly object _syncLock = new object();
        
        public TaskSemaphoreQueue(int runningTasksLimit)
        {
            if (runningTasksLimit <= 0) throw new ArgumentException(nameof(runningTasksLimit));
            RunningTasksLimit = runningTasksLimit;
        }

        public int RunningTasksLimit { get; }

        /// <summary>
        /// Enqueues given task if given key is not already in processing (waiting or running).
        /// </summary>
        /// <param name="key">Identification of task.</param>
        /// <param name="data">Args use to create task</param>
        /// <param name="createTaskCall">Task factory</param>
        public void EnqueueIfNotExists(TKey key, TData data, Func<TData, Task> createTaskCall)
        {
            lock(_syncLock)
            {
                if (_all.ContainsKey(key)) return; // already exists

                _waiting.Enqueue(key);
                _all.Add(key, new Item() { Key = key, Data = data, CreateTaskCall = createTaskCall });

                TryRunningTask();
            }
        }

        private void TryRunningTask()
        {
            lock(_syncLock)
            {
                while (true)
                {
                    // all slots used?
                    if (_all.Count - _waiting.Count >= RunningTasksLimit) return;

                    // nothing in queue?
                    if (_waiting.Count == 0) return;

                    // next to process
                    TKey nextKey = _waiting.Dequeue();
                    Item next = _all[nextKey];
                    next.CreateTaskCall(next.Data)
                        .ContinueWith((task) => AfterTaskFinished(next, task));
                }
            }
        }

        private void AfterTaskFinished(Item item, Task task)
        {
            lock (_syncLock)
            {
                if (!_all.Remove(item.Key)) throw new Exception("Internal exception. Should not happen.");
            }

            //  try schedule next
            TryRunningTask();
        }

        /// <summary>
        /// Removes all waiting items from queue that has not been started yet.
        /// </summary>
        public void ClearQueued()
        {
            lock (_syncLock)
            {
                while (_waiting.Any())
                {
                    TKey key = _waiting.Dequeue();
                    if (!_all.Remove(key)) throw new Exception("Internal exception. Should not happen.");
                }
            }
        }
        
        class Item
        {
            public TKey Key { get; set; }
            public TData Data { get; set; }
            public Func<TData, Task> CreateTaskCall { get; set; }
        }
    }
}
