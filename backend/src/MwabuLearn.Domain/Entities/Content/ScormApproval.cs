using MwabuLearn.Domain.Common;

namespace MwabuLearn.Domain.Entities.Content;

// Playback approval is separate from publication and bound to immutable bytes.
public sealed class ScormApproval : BaseEntity
{
    public Guid ContentAssetId { get; set; }
    public ContentAsset ContentAsset { get; set; } = null!;
    public string Checksum { get; set; } = "";
    public Guid ReviewerId { get; set; }
    public string Evidence { get; set; } = "";
    public bool Revoked { get; set; }
}
