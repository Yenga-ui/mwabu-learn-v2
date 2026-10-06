using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Identity;
using MwabuLearn.Infrastructure.Persistence;
using IdentityError = MwabuLearn.Application.Identity.IdentityError;

namespace MwabuLearn.Infrastructure.Identity;

// Uses Identity's hasher to perform comparable password work for unknown/inactive/locked accounts.
public sealed class UnknownAccountPasswordWork(IPasswordHasher<ApplicationUser> hasher)
{
    private readonly ApplicationUser dummy = new();
    private readonly string hash = hasher.HashPassword(new ApplicationUser(), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    public void Verify(string password) => hasher.VerifyHashedPassword(dummy, hash, password);
}

public sealed class AuthenticationService(MwabuDbContext db, UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn, UnknownAccountPasswordWork unknown, ISessionService sessions) : IAuthenticationService
{
    private static IdentityException Failure() => new(IdentityError.Authentication, "Invalid email or password.");
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256 || string.IsNullOrEmpty(request.Password) || request.Password.Length > 128) throw Failure();
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || await users.IsLockedOutAsync(user))
        {
            unknown.Verify(request.Password);
            throw Failure();
        }
        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded) throw Failure();
        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        if (!(await users.UpdateAsync(user)).Succeeded) throw Failure();
        ct.ThrowIfCancellationRequested();
        return await sessions.CreateAsync(user.Id, ct);
    }
    public async Task<UserResponse> MeAsync(Guid userId, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(x => x.Id == userId && x.IsActive).Select(UserService.Projection).SingleOrDefaultAsync(ct) ?? throw Failure();
    public async Task<IReadOnlyList<UserMembershipResponse>> MyMembershipsAsync(Guid userId, CancellationToken ct) =>
        await db.OrganisationMemberships.AsNoTracking().Where(x => x.UserId == userId).OrderBy(x => x.OrganisationId)
            .Select(x => new UserMembershipResponse(new MembershipResponse(x.Id, x.UserId, x.OrganisationId, x.IsActive, x.JoinedAt, x.CreatedAt, x.UpdatedAt),
                x.Organisation.Name, x.Organisation.IsActive, x.Roles.OrderBy(r => r.Role.Code).Select(r => r.Role.Code).ToList())).ToListAsync(ct);
}
