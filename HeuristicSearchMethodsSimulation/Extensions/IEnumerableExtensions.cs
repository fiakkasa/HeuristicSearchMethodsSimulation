using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HeuristicSearchMethodsSimulation.Extensions
{
    public static class IEnumerableExtensions
    {
        public static ValueTask<List<T>> ToListAsync<T>(this IEnumerable<T> collection, CancellationToken cancellationToken = default) =>
            collection
                .ToAsyncEnumerable()
                .ToListAsync(cancellationToken);
    }
}
