using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Devices;
using MwabuLearn.Application.Identity;
using static MwabuLearn.Infrastructure.Identity.IdentityValidation;
namespace MwabuLearn.Infrastructure.Sync;

public sealed class SyncOptions
{
    public int CursorDays { get; set; } = 30;
    public int MaximumBatchBytes { get; set; } = 2097152;
    public static bool IsValid(SyncOptions o) => o.CursorDays is >= 1 and <= 90 && o.MaximumBatchBytes is >= 131072 and <= 4194304;
}
internal sealed record SyncCursor(int SchemaVersion, Guid UserId, Guid DeviceId, Guid OrganisationId, string Stage,
    int EntityIndex, Guid? LastId, long Version, int Ordinal, long UpperBound, DateTime ExpiresAt);
public sealed class SyncCursorProtector(IDataProtectionProvider provider, IOptions<SyncOptions> options,
    ICurrentUser current, IDeviceContext device)
{
    private readonly IDataProtector protector = provider.CreateProtector("MwabuLearn.Sync.Cursor.v1");
    internal SyncCursor New(long version) => new(1, current.UserId ?? throw Forbidden(), device.DeviceId ?? throw Forbidden(),
        device.OrganisationId ?? throw Forbidden(), "bootstrap", 0, null, version, int.MaxValue, 0, DateTime.UtcNow.AddDays(options.Value.CursorDays));
    internal string Protect(SyncCursor value) => protector.Protect(JsonSerializer.Serialize(value with { ExpiresAt = DateTime.UtcNow.AddDays(options.Value.CursorDays) }));
    internal SyncCursor Read(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor) || cursor.Length > 4096) throw Invalid("A valid sync cursor is required.");
        SyncCursor value;
        try { value = JsonSerializer.Deserialize<SyncCursor>(protector.Unprotect(cursor)) ?? throw new JsonException(); }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException) { throw Invalid("The sync cursor is invalid."); }
        if (value.SchemaVersion != 1 || value.ExpiresAt <= DateTime.UtcNow) throw Conflict("The sync cursor expired or changed schema. Restart bootstrap.");
        if (value.UserId != current.UserId || value.DeviceId != device.DeviceId || value.OrganisationId != device.OrganisationId) throw Forbidden();
        if (value.Version < 0 || value.Ordinal < 0 || value.UpperBound < 0 || value.EntityIndex < 0 || value.EntityIndex > SyncProjection.Types.Length || value.Stage is not ("bootstrap" or "changes"))
            throw Invalid("The sync cursor is invalid.");
        return value;
    }
}
