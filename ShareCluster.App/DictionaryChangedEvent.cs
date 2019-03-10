using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ShareCluster
{
    public struct DictionaryChangedEvent<TKey, TValue>
    {
        public DictionaryChangedEvent(
            IImmutableList<KeyValuePair<TKey, TValue>> added,
            IImmutableList<KeyValuePair<TKey, TValue>> removed,
            IImmutableList<KeyValueChangedPair<TKey, TValue>> changed)
        {
            Added = added;
            Removed = removed;
            Changed = changed;
        }

        public bool HasAnyModifications => (Added?.Count ?? 0) > 0 || (Removed?.Count ?? 0) > 0 || (Changed?.Count ?? 0) > 0;
        public IImmutableList<KeyValuePair<TKey, TValue>> Added { get; }
        public IImmutableList<KeyValuePair<TKey, TValue>> Removed { get; }
        public IImmutableList<KeyValueChangedPair<TKey, TValue>> Changed { get; }
    }
}
