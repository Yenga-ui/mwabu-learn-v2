using MwabuLearn.Domain.Common;

namespace MwabuLearn.Domain.Entities;

public class CurriculumVersion : BaseEntity
{
    private string _name = string.Empty;
    public string Name { get => _name; set { _name = value; NormalizedName = value.ToUpperInvariant(); } }
    public string NormalizedName { get; private set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid CurriculumId { get; set; }
    public Curriculum Curriculum { get; set; } = null!;
    public ICollection<Grade> Grades { get; set; } = new List<Grade>();
}