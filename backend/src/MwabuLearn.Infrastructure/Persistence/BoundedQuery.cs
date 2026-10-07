using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Directories;
namespace MwabuLearn.Infrastructure.Persistence;

internal static class BoundedQuery
{
    public static async Task<List<T>> ToLegacyListAsync<T>(this IQueryable<T> query, CancellationToken ct)
    {
        var rows = await query.Take(1001).ToListAsync(ct);
        if (rows.Count > 1000) throw new LegacyResultLimitException();
        return rows;
    }
}
