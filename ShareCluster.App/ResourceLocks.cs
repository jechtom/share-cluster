using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShareCluster
{
    /// <summary>
    /// Provides logic for locking entity when is in use.
    /// </summary>
    public class ResourceLocks
    {
        private readonly object _syncLock = new object();

        private TaskCompletionSource<object> _deletedFinished;
        private readonly HashSet<object> _tokens = new HashSet<object>();

        public bool IsMarkedToDelete { get; private set; }
        
        public Task MarkForDelete()
        {
            lock(_syncLock)
            {
                if (!IsMarkedToDelete)
                {
                    IsMarkedToDelete = true;
                    _deletedFinished = new TaskCompletionSource<object>();
                    if (_tokens.Count == 0)
                    {
                        _deletedFinished.SetResult(null);
                    }
                }
            }
            return _deletedFinished.Task;
        }

        public bool TryLock(out object token)
        {
            lock (_syncLock)
            {
                if(IsMarkedToDelete)
                {
                    token = null;
                    return false;
                }

                _tokens.Add(token = new object());
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
            lock (_syncLock)
            {
                if (!_tokens.Remove(token)) throw new InvalidOperationException("This lock has been already released.");
                if(_tokens.Count == 0 && IsMarkedToDelete)
                {
                    _deletedFinished.SetResult(null);
                }
            }
        }
    }
}
