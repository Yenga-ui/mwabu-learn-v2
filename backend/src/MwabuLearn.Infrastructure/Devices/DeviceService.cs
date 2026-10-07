using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Devices;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Devices;
using MwabuLearn.Infrastructure.Persistence;
using static MwabuLearn.Infrastructure.Identity.IdentityValidation;
namespace MwabuLearn.Infrastructure.Devices;

public sealed class DeviceOptions
{
    public int CredentialDays { get; set; } = 180;
    public static bool IsValid(DeviceOptions o) => o.CredentialDays is >= 1 and <= 365;
}
public sealed class DeviceService(MwabuDbContext db, ICurrentUser current, IPermissionEvaluator permissions, IOptions<DeviceOptions> options) : IDeviceService
{
    private static DeviceResponse Map(Device x) => new(x.Id, x.OrganisationId, x.RegisteredByUserId, x.ClientRegistrationId,
        x.DisplayName, x.Platform, x.AppVersion, x.IsActive, x.RegisteredAt, x.LastSeenAt, x.RevokedAt, x.CredentialExpiresAt);
    private async Task<Guid> Permit(Guid organisationId, string permission, CancellationToken ct)
    {
        if (current.UserId is not Guid user || !await permissions.CanAsync(user, permission, organisationId, false, ct)) throw Forbidden();
        if (!await db.Organisations.AnyAsync(x => x.Id == organisationId && x.IsActive, ct)) throw Missing("Organisation");
        return user;
    }
    private string Rotate(Device device)
    {
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        device.CredentialHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
        device.CredentialExpiresAt = DateTime.UtcNow.AddDays(options.Value.CredentialDays);
        device.UpdatedAt = DateTime.UtcNow;
        return secret;
    }
    public async Task<DeviceRegistrationResponse> RegisterAsync(Guid organisationId, RegisterDeviceRequest request, CancellationToken ct) => await Transaction(db, async () =>
    {
        var user = await Permit(organisationId, PermissionCodes.ContentRead, ct);
        if (request.ClientRegistrationId == Guid.Empty || !Enum.IsDefined(request.Platform)) throw Invalid("A registration GUID and valid platform are required.");
        var name = Text(request.DisplayName, 100, "Display name");
        var version = string.IsNullOrWhiteSpace(request.AppVersion) ? null : Text(request.AppVersion, 40, "App version");
        var existing = await db.Devices.AsNoTracking().SingleOrDefaultAsync(x => x.OrganisationId == organisationId && x.ClientRegistrationId == request.ClientRegistrationId, ct);
        if (existing is not null)
        {
            if (existing.RegisteredByUserId != user && !await permissions.CanAsync(user, PermissionCodes.MembershipsManage, organisationId, false, ct)) throw Forbidden();
            return new DeviceRegistrationResponse(Map(existing), null, false); // Never persist/replay a raw credential.
        }
        var device = new Device { OrganisationId = organisationId, RegisteredByUserId = user, ClientRegistrationId = request.ClientRegistrationId,
            DisplayName = name, Platform = request.Platform, AppVersion = version };
        var secret = Rotate(device); db.Devices.Add(device); await db.SaveChangesAsync(ct);
        return new DeviceRegistrationResponse(Map(device), secret, true);
    }, ct);
    public async Task<DeviceResponse> GetAsync(Guid organisationId, Guid id, CancellationToken ct)
    {
        await Permit(organisationId, PermissionCodes.ContentRead, ct);
        return Map(await db.Devices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OrganisationId == organisationId, ct) ?? throw Missing("Device"));
    }
    public async Task<DevicePage> ListAsync(Guid organisationId, int page, int pageSize, CancellationToken ct)
    {
        await Permit(organisationId, PermissionCodes.ReportsRead, ct);
        if (page is < 1 or > 100000 || pageSize is < 1 or > 100) throw Invalid("Use a page size from 1 to 100.");
        var devices = await db.Devices.AsNoTracking().Where(x => x.OrganisationId == organisationId).OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1).ToListAsync(ct);
        return new(devices.Take(pageSize).Select(Map).ToList(), page, pageSize, devices.Count > pageSize);
    }
    public async Task<DeviceRegistrationResponse> RotateCredentialAsync(Guid organisationId, Guid id, CancellationToken ct) => await Transaction(db, async () =>
    {
        var user = await Permit(organisationId, PermissionCodes.ContentRead, ct);
        var device = await db.Devices.SingleOrDefaultAsync(x => x.Id == id && x.OrganisationId == organisationId, ct) ?? throw Missing("Device");
        if (!device.IsActive) throw Conflict("Revoked devices cannot rotate credentials.");
        if (device.RegisteredByUserId != user && !await permissions.CanAsync(user, PermissionCodes.MembershipsManage, organisationId, false, ct)) throw Forbidden();
        var secret = Rotate(device); await db.SaveChangesAsync(ct); return new DeviceRegistrationResponse(Map(device), secret, false);
    }, ct);
    public async Task RevokeAsync(Guid organisationId, Guid id, CancellationToken ct) => await Transaction(db, async () =>
    {
        var user = await Permit(organisationId, PermissionCodes.ContentRead, ct);
        var device = await db.Devices.SingleOrDefaultAsync(x => x.Id == id && x.OrganisationId == organisationId, ct) ?? throw Missing("Device");
        if (device.RegisteredByUserId != user && !await permissions.CanAsync(user, PermissionCodes.MembershipsManage, organisationId, false, ct)) throw Forbidden();
        if (device.IsActive) { device.IsActive = false; device.RevokedAt = device.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); }
        return true;
    }, ct);
    public async Task<DeviceResponse?> ValidateAsync(Guid userId, Guid id, string credential, CancellationToken ct)
    {
        if (credential is not { Length: 88 } || credential.Any(char.IsControl)) return null;
        var device = await db.Devices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive && x.CredentialExpiresAt > DateTime.UtcNow &&
            db.Organisations.Any(o => o.Id == x.OrganisationId && o.IsActive) && db.Users.Any(u => u.Id == userId && u.IsActive) &&
            db.OrganisationMemberships.Any(m => m.UserId == userId && m.OrganisationId == x.OrganisationId && m.IsActive), ct);
        if (device is null || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(device.CredentialHash),
            Encoding.ASCII.GetBytes(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credential))))) ||
            !await permissions.CanAsync(userId, PermissionCodes.ContentRead, device.OrganisationId, false, ct)) return null;
        if (device.LastSeenAt is null || device.LastSeenAt < DateTime.UtcNow.AddMinutes(-15))
            await db.Devices.Where(x => x.Id == id && x.IsActive).ExecuteUpdateAsync(s => s.SetProperty(x => x.LastSeenAt, DateTime.UtcNow), ct);
        return Map(device);
    }
}
