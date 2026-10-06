namespace MwabuLearn.Application.Content;

public enum ContentError { Validation, NotFound, Conflict }
public sealed class ContentException(ContentError error, string message) : Exception(message)
{
    public ContentError Error { get; } = error;
}
