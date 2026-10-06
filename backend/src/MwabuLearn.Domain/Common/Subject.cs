using MwabuLearn.Domain.Common;

namespace MwabuLearn.Domain.Entities;

public class Subject : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Code { get; set; }

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public Guid GradeId { get; set; }

    public Grade Grade { get; set; } = null!;
}