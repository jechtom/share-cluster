using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Synchronization
{
    public class ConcurrentHashSet<T>
    {
        ConcurrentDictionary<T, object> _inner = new ConcurrentDictionary<T, object>();

        public void Clear() => _inner.Clear();
        public bool Contains(T item) => _inner.ContainsKey(item);
        public bool Add(T item) => _inner.TryAdd(item, null);
        public int Count => _inner.Count;
    }
}
