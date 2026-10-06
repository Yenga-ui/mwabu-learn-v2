using MwabuLearn.Domain.Common;

namespace MwabuLearn.Domain.Entities.Content;

public sealed class Tag : BaseEntity
{
    private string name = string.Empty;
    public string Name { get => name; set { name = value; NormalizedName = value.ToUpperInvariant(); } }
    public string NormalizedName { get; private set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public ICollection<ContentTag> Contents { get; set; } = new List<ContentTag>();
}

public sealed class ContentTag : BaseEntity
{
    public Guid ContentItemId { get; set; }
    public ContentItem ContentItem { get; set; } = null!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
