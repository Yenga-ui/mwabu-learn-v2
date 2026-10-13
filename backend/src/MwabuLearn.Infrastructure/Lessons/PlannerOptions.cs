namespace MwabuLearn.Infrastructure.Lessons;

public sealed class LessonAiOptions
{
    public bool Enabled { get; set; }
    public bool DataProcessingApproved { get; set; }
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 90;
    public int MaxInputCharacters { get; set; } = 40000;
    public int MaxOutputTokens { get; set; } = 8000;
    public int MaximumAttempts { get; set; } = 2;
    public int MinimumRequestIntervalSeconds { get; set; } = 10;
    public decimal InputUsdPerMillionTokens { get; set; }
    public decimal OutputUsdPerMillionTokens { get; set; }
    public static bool Valid(LessonAiOptions x) => x.TimeoutSeconds is >= 5 and <= 180 && x.MaxInputCharacters is >= 1000 and <= 100000 &&
        x.MaxOutputTokens is >= 500 and <= 16000 && x.MaximumAttempts is >= 1 and <= 3 && x.MinimumRequestIntervalSeconds is >= 1 and <= 60 &&
        (!x.Enabled || x.DataProcessingApproved && x.ApiKey.Length >= 20 && x.Model.Length is > 0 and <= 100 &&
            x.InputUsdPerMillionTokens > 0 && x.OutputUsdPerMillionTokens > 0);
}
public sealed class LessonSearchOptions
{
    public bool Enabled { get; set; }
    public bool DataProcessingApproved { get; set; }
    public string ApiKey { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 15;
    public int CacheMinutes { get; set; } = 10;
    public string[] PreferredDomains { get; set; } = [];
    public static bool Valid(LessonSearchOptions x) => x.TimeoutSeconds is >= 2 and <= 30 && x.CacheMinutes is >= 0 and <= 60 &&
        x.PreferredDomains.Length <= 50 && (!x.Enabled || x.DataProcessingApproved && x.ApiKey.Length >= 10);
}
public sealed class SourceRetrievalOptions
{
    public int TimeoutSeconds { get; set; } = 12;
    public int MaxBytes { get; set; } = 2 * 1024 * 1024;
    public int MaximumRedirects { get; set; } = 3;
    public int MaxExtractedCharacters { get; set; } = 20000;
    public string[] BlockedDomains { get; set; } = [];
    public static bool Valid(SourceRetrievalOptions x) => x.TimeoutSeconds is >= 2 and <= 30 && x.MaxBytes is >= 1024 and <= 5242880 &&
        x.MaximumRedirects is >= 0 and <= 5 && x.MaxExtractedCharacters is >= 1000 and <= 20000 && x.BlockedDomains.Length <= 100;
}
public sealed class LessonPdfOptions { public string? FontPath { get; set; } public string? BoldFontPath { get; set; } }
