using MwabuLearn.Domain.Common;

namespace MwabuLearn.Domain.Entities.Content;

public sealed class Collection : BaseEntity
{
    private string name = string.Empty;
    public string Name { get => name; set { name = value; NormalizedName = value.ToUpperInvariant(); } }
    public string NormalizedName { get; private set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ContentCollection> Contents { get; set; } = new List<ContentCollection>();
}

public sealed class ContentCollection : BaseEntity
{
    public Guid ContentItemId { get; set; }
    public ContentItem ContentItem { get; set; } = null!;
    public Guid CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;
    public int SortOrder { get; set; }
}
