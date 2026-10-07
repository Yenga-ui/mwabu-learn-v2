using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Directories;
using MwabuLearn.Application.Identity;
using static MwabuLearn.Infrastructure.Identity.IdentityValidation;
namespace MwabuLearn.Infrastructure.Education;

internal static class EducationQueries
{
    public static async Task<Page<T>> PageAsync<T>(this IQueryable<T> query, PageRequest page, CancellationToken ct)
    {
        if (page.Page is < 1 or > 100000 || page.PageSize is < 1 or > 100 || page.Text?.Length > 200) throw Invalid("Invalid search or pagination.");
        var rows = await query.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize + 1).ToListAsync(ct);
        return new(rows.Take(page.PageSize).ToList(), page.Page, page.PageSize, rows.Count > page.PageSize);
    }
    public static async Task DemandAsync(this IPermissionEvaluator permissions, ICurrentUser current, string code, Guid? organisation, CancellationToken ct)
    {
        if (current.UserId is not Guid user || !await permissions.CanAsync(user, code, organisation, !organisation.HasValue, ct)) throw Forbidden();
    }
}
