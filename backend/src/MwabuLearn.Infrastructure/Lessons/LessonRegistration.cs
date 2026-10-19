using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Infrastructure.Lessons.Providers;
using MwabuLearn.Infrastructure.Lessons.Sources;
namespace MwabuLearn.Infrastructure.Lessons;

public static class LessonRegistration
{
    public static IServiceCollection AddLessonPlanning(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LessonAiOptions>().BindConfiguration("LessonPlanner:Ai").Validate(LessonAiOptions.Valid, "Invalid lesson AI configuration. Explicit processing approval, model, key and pricing are required when enabled.").ValidateOnStart();
        services.AddOptions<LessonSearchOptions>().BindConfiguration("LessonPlanner:Search").Validate(LessonSearchOptions.Valid, "Invalid lesson search configuration or missing processing approval.").ValidateOnStart();
        services.AddOptions<SourceRetrievalOptions>().BindConfiguration("LessonPlanner:Retrieval").Validate(SourceRetrievalOptions.Valid, "Invalid safe retrieval limits.").ValidateOnStart();
        services.Configure<LessonPdfOptions>(configuration.GetSection("LessonPlanner:Pdf"));
        services.AddMemoryCache();
        services.AddSingleton<SearchResultCache>();
        services.AddHttpClient<OpenAiLessonProvider>(client => client.Timeout = Timeout.InfiniteTimeSpan).RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false });
        services.AddHttpClient<OllamaLessonProvider>(client => client.Timeout = Timeout.InfiniteTimeSpan).RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false });
        services.AddScoped<ILessonAiProvider>(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LessonAiOptions>>().Value.Provider == "OpenAI"
            ? sp.GetRequiredService<OpenAiLessonProvider>() : sp.GetRequiredService<OllamaLessonProvider>());
        services.AddHttpClient<IEducationSearchProvider, BraveEducationSearch>(client => client.Timeout = Timeout.InfiniteTimeSpan).RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false });
        services.AddSingleton<IPublicDns, PublicDns>(); services.AddScoped<PublicUrlGuard>(); services.AddScoped<IPublicSourceRetriever, PublicSourceRetriever>();
        services.AddScoped<IPublicSourceTransport, PublicSourceTransport>();
        services.AddScoped<DocumentExtractor>(); services.AddScoped<LessonAccess>(); services.AddScoped<CurriculumGrounding>();
        services.AddSingleton<ILessonPdf, LessonPdf>(); services.AddScoped<ILessonPlanner, LessonPlanner>(); services.AddScoped<ILessonSources, LessonSources>();
        services.AddScoped<ILessonGeneration, LessonGenerationService>(); services.AddScoped<LessonRetention>(); services.AddHostedService<LessonRetentionWorker>();
        services.AddScoped<ILessonSyllabus, LessonSyllabus>();
        services.AddScoped<LessonJobActor>(); services.AddScoped<LessonGenerationJobs>();
        services.AddScoped<LessonGenerationJobRunner>(); services.AddHostedService<LessonGenerationWorker>();
        return services;
    }
}
