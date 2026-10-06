using MwabuLearn.Domain.Common;

namespace MwabuLearn.Domain.Entities;

public class Curriculum : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string CountryCode { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Grade> Grades { get; set; } = new List<Grade>();
}