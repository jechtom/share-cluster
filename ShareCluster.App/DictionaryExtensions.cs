using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace ShareCluster
{
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Does replacement of values in immutable dictionary and returns new immutable dictionary instance and changed event.
        /// </summary>
        /// <typeparam name="TKey">Type of key in dictionary.</typeparam>
        /// <typeparam name="TValue">Type of value - must implement <see cref="IEquatable{T}"/> to support ignoring unchanged data.</typeparam>
        /// <param name="items">Current immutable dictionary.</param>
        /// <param name="newValues">New values to set in given dictionary.</param>
        /// <returns>Returns new immutable dictionary instance and change event</returns>
        public static (IImmutableDictionary<TKey, TValue>, DictionaryChangedEvent<TKey, TValue>) ReplaceWithAndGetEvent<TKey, TValue>(this IImmutableDictionary<TKey, TValue> items, IEnumerable<KeyValuePair<TKey, TValue>> newValues) where TValue : IEquatable<TValue>
        {
            IList<KeyValuePair<TKey, TValue>> added = null;
            IList<KeyValuePair<TKey, TValue>> removed = null;
            IList<KeyValueChangedPair<TKey, TValue>> changed = null;

            int count = 0;
            foreach (KeyValuePair<TKey,TValue> item in newValues)
            {
                count++;

                if (items.TryGetValue(item.Key, out TValue current))
                {
                    // update not required?
                    if (current.Equals(item.Value)) continue;

                    // do update
                    (changed = changed ?? new List<KeyValueChangedPair<TKey, TValue>>())
                        .Add(new KeyValueChangedPair<TKey, TValue>(item, current));
                }
                else
                {
                    // new item
                    (added = added ?? new List<KeyValuePair<TKey, TValue>>())
                        .Add(item);
                }
            }

            // should we clear excess items?
            // optimization: look for excess items only if final count (after adding missing items) is greater than desired size
            bool clearExcess = items.Count + (added?.Count ?? 0) > count;

            if (clearExcess)
            {
                var expectedIds = newValues.Select(v => v.Key).ToHashSet();

                removed = items
                    .Where(k => !expectedIds.Contains(k.Key))
                    .ToList();
            }

            // update dictionary
            if(removed != null) items = items.RemoveRange(removed.Select(r => r.Key));
            if(changed != null) items = items.SetItems(changed.Select(c => c.NewItem));
            if(added != null) items = items.AddRange(added);

            // notify
            var changeEvent = DictionaryChangedEvent<TKey, TValue>
                .FromNullableEnumerable(added: added, changed: changed, removed: removed);

            return (items, changeEvent);
        }
    }
}
