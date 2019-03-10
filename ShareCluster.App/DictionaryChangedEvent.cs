using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ShareCluster
{
    /// <summary>
    /// Describes changes in collection.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
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

        /// <summary>
        /// Creates instance from given <see cref="IEnumerable{T}"/>. If null is passed, empty immutable list is used.
        /// </summary>
        /// <param name="added">If null is passed, empty immutable list is used.</param>
        /// <param name="removed">If null is passed, empty immutable list is used.</param>
        /// <param name="changed">If null is passed, empty immutable list is used.</param>
        /// <returns></returns>
        public static DictionaryChangedEvent<TKey, TValue> FromNullableEnumerable(
            IEnumerable<KeyValuePair<TKey, TValue>> added,
            IEnumerable<KeyValuePair<TKey, TValue>> removed,
            IEnumerable<KeyValueChangedPair<TKey, TValue>> changed
        ) =>
            new DictionaryChangedEvent<TKey, TValue>(
                added: added == null ? ImmutableArray<KeyValuePair<TKey, TValue>>.Empty : added.ToImmutableArray(),
                changed: changed == null ? ImmutableArray<KeyValueChangedPair<TKey, TValue>>.Empty : changed.ToImmutableArray(),
                removed: removed == null ? ImmutableArray<KeyValuePair<TKey, TValue>>.Empty : removed.ToImmutableArray()
            );

        public bool HasAnyModifications => (Added?.Count ?? 0) > 0 || (Removed?.Count ?? 0) > 0 || (Changed?.Count ?? 0) > 0;
        public IImmutableList<KeyValuePair<TKey, TValue>> Added { get; }
        public IImmutableList<KeyValuePair<TKey, TValue>> Removed { get; }
        public IImmutableList<KeyValueChangedPair<TKey, TValue>> Changed { get; }
    }
}
