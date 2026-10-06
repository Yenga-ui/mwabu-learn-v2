using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Identity;
using MwabuLearn.Infrastructure.Persistence;
using Npgsql;
using IdentityError = MwabuLearn.Application.Identity.IdentityError;

namespace MwabuLearn.Infrastructure.Identity;

internal static class IdentityValidation
{
    public static string Text(string? value, int length, string label)
    {
        var result = value?.Trim() ?? "";
        if (result.Length == 0 || result.Length > length || result.Any(char.IsControl)) throw Invalid($"{label} is required (maximum {length} characters, no control characters).");
        return result;
    }
    public static string Email(string? value)
    {
        var email = Text(value, 256, "Email");
        if (!new EmailAddressAttribute().IsValid(email)) throw Invalid("A valid email address is required.");
        return email;
    }
    public static string? Phone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var phone = Text(value, 30, "Phone number");
        if (phone.Any(c => !char.IsDigit(c) && c is not ('+' or '-' or ' ' or '(' or ')'))) throw Invalid("Invalid phone number format.");
        return phone;
    }
    public static void Result(IdentityResult result)
    {
        if (result.Succeeded) return;
        if (result.Errors.Any(x => x.Code is "DuplicateEmail" or "DuplicateUserName" or "ConcurrencyFailure")) throw Conflict("The user already exists or was modified concurrently.");
        // Identity descriptions are not echoed: custom validators could embed email or other PII.
        throw Invalid("User details or password do not satisfy the configured Identity policies.");
    }
    public static IdentityException Invalid(string message) => new(IdentityError.Validation, message);
    public static IdentityException Missing(string name) => new(IdentityError.NotFound, $"{name} was not found.");
    public static IdentityException Conflict(string message) => new(IdentityError.Conflict, message);
    public static IdentityException Forbidden() => new(IdentityError.Forbidden, "This operation is not permitted.");
    public static async Task<T> Transaction<T>(MwabuDbContext db, Func<Task<T>> action, CancellationToken ct)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var result = await action();
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateConcurrencyException) { throw Conflict("The record changed concurrently. Reload and retry."); }
        catch (Exception ex) when (Postgres(ex) is { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected })
        { throw Conflict("A concurrent operation conflicted. Reload and retry."); }
        catch (DbUpdateException ex) when (Postgres(ex) is { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw Conflict("This user, organisation code, membership or role assignment already exists."); }
        catch (DbUpdateException ex) when (Postgres(ex) is { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        { throw Conflict("A referenced record changed. Reload and retry."); }
    }
    private static PostgresException? Postgres(Exception error) => error as PostgresException ?? error.InnerException as PostgresException;
}

public sealed class PlatformAdministratorGuard(MwabuDbContext db)
{
    public async Task PreserveAsync(Guid? excludingUser, Guid? excludingOrganisation, Guid? excludingMembership,
        Guid? excludingAssignment, CancellationToken ct)
    {
        // If an active administrator exists, a state change cannot remove the last one.
        var admins = db.OrganisationMembershipRoles.Where(x => x.Role.GrantsPlatformAuthority && x.Membership.IsActive &&
            x.Membership.Organisation.IsActive && x.Membership.Organisation.OrganisationType == Domain.Entities.Organisations.OrganisationType.Platform &&
            db.Users.Any(u => u.Id == x.Membership.UserId && u.IsActive));
        if (await admins.AnyAsync(ct) && !await admins.AnyAsync(x =>
            (excludingUser == null || x.Membership.UserId != excludingUser) &&
            (excludingOrganisation == null || x.Membership.OrganisationId != excludingOrganisation) &&
            (excludingMembership == null || x.OrganisationMembershipId != excludingMembership) &&
            (excludingAssignment == null || x.Id != excludingAssignment), ct))
            throw IdentityValidation.Conflict("This operation would remove the last active platform administrator.");
    }
}
