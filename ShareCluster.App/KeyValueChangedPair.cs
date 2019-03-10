using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster
{
    public struct KeyValueChangedPair<TKey, TValue>
    {
        public KeyValuePair<TKey, TValue> NewItem { get; }
        public TValue OldValue { get; }

        public KeyValueChangedPair(KeyValuePair<TKey, TValue> newItem, TValue oldValue)
        {
            NewItem = newItem;
            OldValue = oldValue;
        }
    }
}
