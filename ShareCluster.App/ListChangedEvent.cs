using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace ShareCluster
{
    /// <summary>
    /// Describes changes in collection.
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    public struct ListChangedEvent<TValue>
    {
        public ListChangedEvent(
            IImmutableList<TValue> added,
            IImmutableList<TValue> removed)
        {
            Added = added;
            Removed = removed;
        }

        /// <summary>
        /// Creates instance from given <see cref="IEnumerable{T}"/>. If null is passed, empty immutable list is used.
        /// </summary>
        /// <param name="added">If null is passed, empty immutable list is used.</param>
        /// <param name="removed">If null is passed, empty immutable list is used.</param>
        /// <param name="changed">If null is passed, empty immutable list is used.</param>
        /// <returns></returns>
        public static ListChangedEvent<TValue> FromNullableEnumerable(
            IEnumerable<TValue> added,
            IEnumerable<TValue> removed
        ) =>
            new ListChangedEvent<TValue>(
                added: added == null ? ImmutableArray<TValue>.Empty : added.ToImmutableArray(),
                removed: removed == null ? ImmutableArray<TValue>.Empty : removed.ToImmutableArray()
            );

        public bool HasAnyModifications => (Added?.Count ?? 0) > 0 || (Removed?.Count ?? 0) > 0;
        public IImmutableList<TValue> Added { get; }
        public IImmutableList<TValue> Removed { get; }
    }
}
