using System.Text.Json;
using System.Text.Json.Serialization;
namespace MwabuLearn.Application.Content;

public sealed record ActivityQuestion(string Prompt, string[] Options, int CorrectOption, string Explanation);
public sealed record ActivityDefinition(int SchemaVersion, string Kind, string Title, string Instructions, string Language, ActivityQuestion[] Questions, int Number = 0);
public sealed record ActivityResource(ActivityDefinition Definition, string Checksum, Guid AssetId);
public static class ActivityRules
{
    public const string MimeType = "application/vnd.mwabu.activity+json";
    public const int MaxBytes = 100000;
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 8 };
    public static ActivityDefinition Validate(ActivityDefinition value)
    {
        static bool Text(string? text, int limit) => !string.IsNullOrWhiteSpace(text) && text.Length <= limit && !text.Any(char.IsControl);
        if (value is null || value.SchemaVersion != 1 || value.Kind is not ("quiz" or "place-value") || !Text(value.Title, 200) || !Text(value.Instructions, 2000) || !Text(value.Language, 35) || value.Questions is null || value.Number is < 0 or > 9999)
            throw new ContentException(ContentError.Validation, "Use a supported activity, title, instructions, language and a number from 0 to 9999.");
        if (value.Kind == "quiz")
        {
            if (value.Questions.Length is < 1 or > 10 || value.Questions.Any(q => q is null || !Text(q.Prompt, 1000) || !Text(q.Explanation, 2000) || q.Options is null || q.Options.Length is < 2 or > 6 || q.Options.Any(x => !Text(x, 300)) || q.Options.Select(x => x.Trim().ToUpperInvariant()).Distinct().Count() != q.Options.Length || q.CorrectOption < 0 || q.CorrectOption >= q.Options.Length))
                throw new ContentException(ContentError.Validation, "Use 1-10 questions, distinct answer choices, one valid correct choice and explanatory feedback.");
        }
        else if (value.Questions.Length != 0) throw new ContentException(ContentError.Validation, "A place-value animation cannot contain quiz questions.");
        if (JsonSerializer.SerializeToUtf8Bytes(value, Json).Length > MaxBytes) throw new ContentException(ContentError.Validation, "Activity exceeds its size limit.");
        return value;
    }
}
