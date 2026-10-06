using MwabuLearn.Domain.Common;

namespace MwabuLearn.Domain.Entities;

public class Grade : BaseEntity
{
    private string _name = string.Empty;
    public string Name { get => _name; set { _name = value; NormalizedName = value.ToUpperInvariant(); } }
    public string NormalizedName { get; private set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid CurriculumVersionId { get; set; }
    public CurriculumVersion CurriculumVersion { get; set; } = null!;
    public ICollection<Subject> Subjects { get; set; } = new List<Subject>();
}