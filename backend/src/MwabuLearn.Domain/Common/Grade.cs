using MwabuLearn.Domain.Common;

namespace MwabuLearn.Domain.Entities;

public class Grade : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public Guid CurriculumId { get; set; }

    public Curriculum Curriculum { get; set; } = null!;

    public ICollection<Subject> Subjects { get; set; } = new List<Subject>();
}