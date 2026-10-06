using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Identity;
using MwabuLearn.Infrastructure.Persistence;
using static MwabuLearn.Infrastructure.Identity.IdentityValidation;

namespace MwabuLearn.Infrastructure.Identity;

public sealed class UserService(MwabuDbContext db, UserManager<ApplicationUser> users, PlatformAdministratorGuard guard) : IUserService
{
    internal static readonly Expression<Func<ApplicationUser, UserResponse>> Projection = x => new(x.Id, x.Email!, x.UserName!,
        x.FirstName, x.LastName, x.PhoneNumber, x.IsActive, x.LastLoginAt, x.CreatedAt, x.UpdatedAt);
    internal static UserResponse Map(ApplicationUser x) => new(x.Id, x.Email!, x.UserName!, x.FirstName, x.LastName,
        x.PhoneNumber, x.IsActive, x.LastLoginAt, x.CreatedAt, x.UpdatedAt);
    public async Task<UserPage> ListAsync(UserSearchRequest request, CancellationToken ct)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 100 || (long)(request.Page - 1) * request.PageSize > int.MaxValue)
            throw Invalid("Invalid pagination; page size must be between 1 and 100.");
        var query = db.Users.AsNoTracking();
        var count = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Id).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).Select(Projection).ToListAsync(ct);
        return new UserPage(items, request.Page, request.PageSize, count);
    }
    public async Task<UserResponse> GetAsync(Guid id, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(x => x.Id == id).Select(Projection).SingleOrDefaultAsync(ct) ?? throw Missing("User");
    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct) => await Transaction(db, async () =>
    {
        var email = Email(request.Email);
        var entity = new ApplicationUser
        {
            Email = email, UserName = email, FirstName = Text(request.FirstName, 100, "First name"),
            LastName = Text(request.LastName, 100, "Last name"), PhoneNumber = Phone(request.PhoneNumber), LockoutEnabled = true
        };
        if (string.IsNullOrEmpty(request.InitialPassword) || request.InitialPassword.Length > 128) throw Invalid("An initial password of at most 128 characters is required.");
        if (await db.Users.AnyAsync(x => x.NormalizedEmail == users.NormalizeEmail(email), ct)) throw Conflict("The email already exists.");
        ct.ThrowIfCancellationRequested();
        Result(await users.CreateAsync(entity, request.InitialPassword));
        ct.ThrowIfCancellationRequested();
        return Map(entity);
    }, ct);
    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct) => await Transaction(db, async () =>
    {
        var entity = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing("User");
        if (!isActive) await guard.PreserveAsync(id, null, null, null, ct);
        if (entity.IsActive != isActive)
        {
            entity.IsActive = isActive;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.AccessTokenVersion = checked(entity.AccessTokenVersion + 1);
            Result(await users.UpdateSecurityStampAsync(entity));
        }
        return true;
    }, ct);
}
