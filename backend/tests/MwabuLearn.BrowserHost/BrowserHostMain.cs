using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Infrastructure.Lessons.Sources;
using MwabuLearn.Infrastructure.Lessons;
using Npgsql;
namespace MwabuLearn.BrowserHost;

// Test-only executable, never referenced or copied by the production API/Docker image.
public static class BrowserHostMain
{
    public static async Task Main()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ConnectionStrings__MwabuLearnDb"));
        if (Environment.GetEnvironmentVariable("MWABU_BROWSER_TEST_HOST") != "true" || connection.Database?.StartsWith("mwabu_e2e_", StringComparison.Ordinal) != true ||
            connection.Host is not ("127.0.0.1" or "localhost" or "::1")) throw new InvalidOperationException("Browser host requires an explicitly enabled isolated loopback mwabu_e2e_ database.");
        using var certificate = X509CertificateLoader.LoadPkcs12FromFile(Environment.GetEnvironmentVariable("Kestrel__Certificates__Default__Path")!,
            Environment.GetEnvironmentVariable("Kestrel__Certificates__Default__Password"), OperatingSystem.IsWindows() ? X509KeyStorageFlags.DefaultKeySet : X509KeyStorageFlags.EphemeralKeySet);
        // Kestrel must use the explicitly loaded disposable test key, rather than importing it again into a Windows profile key store.
        Environment.SetEnvironmentVariable("Kestrel__Certificates__Default__Path", null);
        Environment.SetEnvironmentVariable("Kestrel__Certificates__Default__Password", null);
        await using var factory = new BrowserFactory();
        factory.UseKestrel(options => options.Listen(IPAddress.Loopback, 7443, listen => listen.UseHttps(certificate)));
        factory.StartServer();
        Console.WriteLine("Real API/HTTPS browser fixture ready; external services use explicitly controlled TEST-ONLY providers.");
        using var stop = new CancellationTokenSource(); Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; stop.Cancel(); };
        // ConsoleLifetime handles SIGTERM on Linux. Main must observe the host's shutdown request too.
        using var shutdown = factory.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.Register(stop.Cancel);
        try { await Task.Delay(Timeout.InfiniteTimeSpan, stop.Token); } catch (OperationCanceledException) { }
    }
}
internal sealed class BrowserFactory : WebApplicationFactory<global::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development"); builder.UseContentRoot(Environment.GetEnvironmentVariable("MWABU_BROWSER_API_ROOT")!);
        builder.UseWebRoot(Environment.GetEnvironmentVariable("MWABU_BROWSER_WEB_ROOT")!);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Kestrel:Certificates:Default:Path"] = null, ["Kestrel:Certificates:Default:Password"] = null }));
        builder.ConfigureServices(services =>
        {
            services.Configure<LessonAiOptions>(options => options.MinimumRequestIntervalSeconds = 1);
            services.RemoveAll<ILessonAiProvider>(); services.AddSingleton<ILessonAiProvider, ControlledAi>();
            services.RemoveAll<IEducationSearchProvider>(); services.AddSingleton<IEducationSearchProvider, ControlledSearch>();
            services.RemoveAll<IPublicSourceRetriever>(); services.AddSingleton<IPublicSourceRetriever, ControlledRetrieval>();
            services.RemoveAll<IPublicDns>(); services.AddSingleton<IPublicDns, ControlledDns>();
        });
    }
}
internal sealed class ControlledDns : IPublicDns
{ public Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }); }
internal sealed class ControlledSearch : IEducationSearchProvider
{
    public bool IsConfigured => true;
    public Task<IReadOnlyList<DiscoveredSource>> SearchAsync(string query, string language, CancellationToken ct) => Task.FromResult<IReadOnlyList<DiscoveredSource>>(
        [new("Controlled classroom fractions source", "https://education.example.org/controlled-fractions", "Deterministic search metadata for browser acceptance; not live internet research.", "Controlled test publisher", null, 1)]);
}
internal sealed class ControlledRetrieval : IPublicSourceRetriever
{
    public Task<RetrievedSource> RetrieveAsync(string url, CancellationToken ct) => Task.FromResult(new RetrievedSource(
        "Controlled classroom fractions source", "Controlled test publisher", "https://creativecommons.org/licenses/by/4.0/", "Test fixture teaching evidence: folded paper demonstrates two equal shares.", true,
        "Controlled browser-test excerpt, explicitly marked test-only; real safe retrieval is covered by backend transport/security tests.", url));
}
internal sealed class ControlledAi : ILessonAiProvider
{
    public bool IsConfigured => true; public string Name => "ControlledTestOnly"; public string Model => "deterministic-browser-fixture";
    public async Task<ProviderResult> GenerateAsync(ProviderContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (context.Instructions.Contains("simulate-slow-job", StringComparison.Ordinal)) await Task.Delay(TimeSpan.FromSeconds(8), ct);
        if (context.Instructions.Contains("simulate-provider-failure", StringComparison.Ordinal)) throw new PlannerException(PlannerError.Unavailable, "Controlled test provider failure; draft unchanged.");
        var sections = context.Lesson.Sections.Where(x => context.SectionCode is null || x.Code == context.SectionCode).Select(section =>
        {
            if (LessonRules.Authoritative.Contains(section.Code)) return section;
            var body = context.SelectedText is not null ? section.Body.Replace(context.SelectedText, context.SelectedText + " (simplified example)", StringComparison.Ordinal) : section.Code switch
            {
                "objectives" => "By the end of this lesson, learners will identify one half and explain equal shares using two familiar objects.",
                "development" => "Model folding paper into two equal parts. Learners compare and explain their halves. Discuss unequal shares and check understanding.",
                "starter" => "Invite learners to share a familiar object equally and explain their choices.",
                "conclusion" => "Learners explain one half using an example; teacher checks the lesson objective.",
                _ => "Controlled test-only teaching proposal: " + section.Title + ". Review with the teacher before use."
            };
            return section with { Body = body, Minutes = context.SectionCode is not null ? section.Minutes : section.Code == "starter" ? 5 : section.Code == "development" ? context.Lesson.Setup.DurationMinutes - 10 : section.Code == "conclusion" ? 5 : 0,
                SourceIds = section.Code == "development" ? context.Evidence.Select(x => x.SourceId).Take(2).ToArray() : [] };
        }).ToArray();
        return new ProviderResult(new("Deterministic TEST-ONLY proposal for browser acceptance. No real external AI call was made.", sections), 100, 300);
    }
}
