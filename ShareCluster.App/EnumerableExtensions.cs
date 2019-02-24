using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ShareCluster
{
    /// <summary>
    /// Provides LINQ extensions.
    /// </summary>
    /// <remarks>
    /// Sources:
    /// - https://stackoverflow.com/questions/5489987/linq-full-outer-join
    /// </remarks>
    public static class EnumerableExtensions
    {
        public static IEnumerable<TResult> FullOuterGroupJoin<TA, TB, TKey, TResult>(
            this IEnumerable<TA> a,
            IEnumerable<TB> b,
            Func<TA, TKey> selectKeyA,
            Func<TB, TKey> selectKeyB,
            Func<IEnumerable<TA>, IEnumerable<TB>, TKey, TResult> projection,
            IEqualityComparer<TKey> cmp = null)
        {
            cmp = cmp ?? EqualityComparer<TKey>.Default;
            ILookup<TKey, TA> alookup = a.ToLookup(selectKeyA, cmp);
            ILookup<TKey, TB> blookup = b.ToLookup(selectKeyB, cmp);

            var keys = new HashSet<TKey>(alookup.Select(p => p.Key), cmp);
            keys.UnionWith(blookup.Select(p => p.Key));

            IEnumerable<TResult> join = from key in keys
                       let xa = alookup[key]
                       let xb = blookup[key]
                       select projection(xa, xb, key);

            return join;
        }

        public static IEnumerable<TResult> FullOuterJoin<TA, TB, TKey, TResult>(
            this IEnumerable<TA> a,
            IEnumerable<TB> b,
            Func<TA, TKey> selectKeyA,
            Func<TB, TKey> selectKeyB,
            Func<TA, TB, TKey, TResult> projection,
            TA defaultA = default(TA),
            TB defaultB = default(TB),
            IEqualityComparer<TKey> cmp = null)
        {
            cmp = cmp ?? EqualityComparer<TKey>.Default;
            ILookup<TKey, TA> alookup = a.ToLookup(selectKeyA, cmp);
            ILookup<TKey, TB> blookup = b.ToLookup(selectKeyB, cmp);

            var keys = new HashSet<TKey>(alookup.Select(p => p.Key), cmp);
            keys.UnionWith(blookup.Select(p => p.Key));

            IEnumerable<TResult> join = from key in keys
                       from xa in alookup[key].DefaultIfEmpty(defaultA)
                       from xb in blookup[key].DefaultIfEmpty(defaultB)
                       select projection(xa, xb, key);

            return join;
        }
    }
}
