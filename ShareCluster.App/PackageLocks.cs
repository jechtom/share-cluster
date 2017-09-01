using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShareCluster
{
    /// <summary>
    /// Provides logic for locking package when package data is in use.
    /// </summary>
    public class PackageLocks
    {
        private readonly object syncLock = new object();

        private TaskCompletionSource<object> deletedFinished;
        private readonly HashSet<object> tokens = new HashSet<object>();

        public bool IsMarkedToDelete { get; private set; }
        
        public Task MarkForDelete()
        {
            lock(syncLock)
            {
                if (!IsMarkedToDelete)
                {
                    IsMarkedToDelete = true;
                    deletedFinished = new TaskCompletionSource<object>();
                    if (tokens.Count == 0)
                    {
                        deletedFinished.SetResult(null);
                    }
                }
            }
            return deletedFinished.Task;
        }

        public bool TryLock(out object token)
        {
            lock (syncLock)
            {
                if(IsMarkedToDelete)
                {
                    token = null;
                    return false;
                }

                tokens.Add(token = new object());
                return true;
            }
        }

        public object Lock()
        {
            if(!TryLock(out object result))
            {
                throw new InvalidOperationException("No more locks can be created for this resource. It is marked to deleted.");
            }
            return result;
        }

        public void Unlock(object token)
        {
            lock (syncLock)
            {
                if (!tokens.Remove(token)) throw new InvalidOperationException("This lock has been already released.");
                if(tokens.Count == 0 && IsMarkedToDelete)
                {
                    deletedFinished.SetResult(null);
                }
            }
        }
    }
}
