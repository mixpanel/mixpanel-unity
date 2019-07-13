using System.Collections.Generic;
using System.Linq;

namespace mixpanel
{
    internal static class Extensions
    {
        internal static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> items, int maxItems)
        {
            return items
                .Select((item, inx) => new { item, inx })
                .GroupBy(x => x.inx / maxItems)
                .Select(g => g.Select(x => x.item));
        }
    }
}