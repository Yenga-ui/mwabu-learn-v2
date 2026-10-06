namespace MwabuLearn.Application.Curricula;

public enum CurriculumError { Validation, NotFound, Conflict }

public sealed class CurriculumException(CurriculumError error, string message) : Exception(message)
{
    public CurriculumError Error { get; } = error;
}