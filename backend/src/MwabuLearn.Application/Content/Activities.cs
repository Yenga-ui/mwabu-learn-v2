using System.Text.Json;
using System.Text.Json.Serialization;
namespace MwabuLearn.Application.Content;

public sealed record ActivityQuestion(string Prompt, string[] Options, int CorrectOption, string Explanation);
public sealed record ActivityDefinition(int SchemaVersion, string Kind, string Title, string Instructions, string Language, ActivityQuestion[] Questions, int Number = 0, int Minimum = 0, int Maximum = 20, int Denominator = 4);
public sealed record ActivityResource(ActivityDefinition Definition, string Checksum, Guid AssetId);
public static class ActivityRules
{
    public const string MimeType = "application/vnd.mwabu.activity+json";
    public const int MaxBytes = 100000;
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 8 };
    public static ActivityDefinition Validate(ActivityDefinition value)
    {
        static bool Text(string? text, int limit) => !string.IsNullOrWhiteSpace(text) && text.Length <= limit && !text.Any(char.IsControl);
        if (value is null || value.SchemaVersion is not (1 or 2) || value.Kind is not ("quiz" or "place-value" or "matching" or "sequencing" or "fill-blank" or "number-line" or "fraction" or "place-value-exercise") || value.SchemaVersion == 1 && value.Kind is not ("quiz" or "place-value") || !Text(value.Title, 200) || !Text(value.Instructions, 2000) || !Text(value.Language, 35) || value.Questions is null || value.Number is < 0 or > 9999)
            throw new ContentException(ContentError.Validation, "Use a supported activity, title, instructions, language and a number from 0 to 9999.");
        if (value.Kind is "quiz" or "matching" or "sequencing" or "fill-blank")
        {
            if (value.Questions.Length is < 1 or > 10 || value.Questions.Any(q => q is null || !Text(q.Prompt, 1000) || !Text(q.Explanation, 2000) || q.Options is null || q.Options.Length is < 2 or > 6 || q.Options.Any(x => !Text(x, 300)) || q.Options.Select(x => x.Trim().ToUpperInvariant()).Distinct().Count() != q.Options.Length || q.CorrectOption < 0 || q.CorrectOption >= q.Options.Length))
                throw new ContentException(ContentError.Validation, "Use 1-10 questions, distinct answer choices, one valid correct choice and explanatory feedback.");
        }
        else if (value.Questions.Length != 0) throw new ContentException(ContentError.Validation, "This mathematics activity cannot contain quiz questions.");
        if (value.Kind == "number-line" && (value.Minimum < -1000 || value.Maximum > 1000 || value.Maximum <= value.Minimum || value.Maximum - value.Minimum > 100 || value.Number < value.Minimum || value.Number > value.Maximum) || value.Kind == "fraction" && (value.Denominator is < 2 or > 24 || value.Number > value.Denominator))
            throw new ContentException(ContentError.Validation, "Use a bounded number line or a fraction between zero and one with 2-24 equal parts.");
        if (JsonSerializer.SerializeToUtf8Bytes(value, Json).Length > MaxBytes) throw new ContentException(ContentError.Validation, "Activity exceeds its size limit.");
        return value;
    }

    // Answer values are bounded strings: option index, ordered option indices, text, or mathematics inputs.
    // Always call against a server-loaded immutable definition, never a definition supplied with answers.
    public static ActivityMark Mark(ActivityDefinition definition, string[][] answers)
    {
        Validate(definition);
        var count = definition.Questions.Length == 0 ? 1 : definition.Questions.Length;
        if (definition.Kind == "place-value") throw new ContentException(ContentError.Validation, "An explanation is practice, not an assessment.");
        if (answers is null || answers.Length != count || answers.Any(x => x is null || x.Length > 24 || x.Any(a => a is null || a.Length > 300)))
            throw new ContentException(ContentError.Validation, "Submit one bounded answer per question.");
        var results = new bool[count];
        for (var i = 0; i < count; i++)
        {
            var answer = answers[i];
            var q = definition.Questions.ElementAtOrDefault(i);
            results[i] = definition.Kind switch
            {
                "quiz" or "matching" => answer.Length == 1 && answer[0] == q!.CorrectOption.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "sequencing" => answer.SequenceEqual(Enumerable.Range(0, q!.Options.Length).Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                "fill-blank" => answer.Length == 1 && q!.Options.Any(x => string.Equals(x.Trim().Normalize(), answer[0].Trim().Normalize(), StringComparison.OrdinalIgnoreCase)),
                "number-line" or "fraction" => answer.Length == 1 && answer[0] == definition.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "place-value-exercise" => answer.SequenceEqual(definition.Number.ToString("D4", System.Globalization.CultureInfo.InvariantCulture).Select(x => x.ToString())),
                _ => false
            };
        }
        return new(results.Count(x => x), count, results);
    }
}
public sealed record ActivityMark(int Score, int Total, bool[] Correct);
