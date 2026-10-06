using MwabuLearn.Domain.Common;

namespace MwabuLearn.Domain.Entities;

public class Topic : BaseEntity
{
    private string _name = string.Empty;
    public string Name { get => _name; set { _name = value; NormalizedName = value.ToUpperInvariant(); } }
    public string NormalizedName { get; private set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid TermId { get; set; }
    public Term Term { get; set; } = null!;
    public ICollection<Competency> Competencies { get; set; } = new List<Competency>();
}