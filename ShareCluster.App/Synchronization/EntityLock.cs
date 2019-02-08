using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShareCluster.Synchronization
{
    /// <summary>
    /// Provides shared locks (allowing multiple locks at the same time) with synchronization to deleted state.
    /// When marked for deletion it will not allow obtaining new shared locks and waits for all shared locks to be released.
    /// </summary>
    public class EntityLock
    {
        private readonly object _syncLock = new object();

        private TaskCompletionSource<object> _deletedFinished;
        private readonly HashSet<object> _tokens = new HashSet<object>();

        public bool IsMarkedToDelete { get; private set; }
        
        public Task MarkForDeletionAsync()
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

        public bool TryObtainSharedLock(out object token)
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

        public object ObtainSharedLock()
        {
            if(!TryObtainSharedLock(out object result))
            {
                throw new InvalidOperationException("No more locks can be created for this resource. It is marked to deleted.");
            }
            return result;
        }

        public void ReleaseSharedLock(object token)
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
