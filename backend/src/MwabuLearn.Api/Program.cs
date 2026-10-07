using MwabuLearn.Api.Operations;

using Microsoft.EntityFrameworkCore;
using MwabuLearn.Infrastructure.Persistence;
using MwabuLearn.Application.Curricula;
using MwabuLearn.Infrastructure.Curricula;
using MwabuLearn.Api.Errors;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using MwabuLearn.Application.Content;
using MwabuLearn.Infrastructure.Content;
using MwabuLearn.Infrastructure.Content.Storage;
using MwabuLearn.Api.Security;
using Microsoft.OpenApi;
var builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------
// Services
// -------------------------------------------------------

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddScoped<IContentService, ContentService>();
builder.Services.AddContentStorage(builder.Configuration);
builder.Services.AddOptions<MwabuLearn.Infrastructure.Operations.BackgroundWorkOptions>().BindConfiguration("BackgroundWork")
    .Validate(MwabuLearn.Infrastructure.Operations.BackgroundWorkOptions.IsValid, "Invalid background work settings.").ValidateOnStart();
builder.Services.AddScoped<MwabuLearn.Infrastructure.Operations.BackgroundJobRunner>();
builder.Services.AddHostedService<MwabuLearn.Infrastructure.Operations.BackgroundWorkService>();
builder.Services.AddOptions<ContentOptions>().BindConfiguration("Content")
    .Validate(x => x.MaxUploadBytes is > 0 and <= 1073741824, "Upload limit must be between 1 byte and 1 GiB.").ValidateOnStart();
builder.Services.AddOptions<LocalContentStorageOptions>().Configure(options =>
{
    options.RootPath = Path.GetFullPath(builder.Configuration["ContentStorage:RootPath"] ?? ".local/content", builder.Environment.ContentRootPath);
});
var uploadLimit = builder.Configuration.GetValue<long?>("Content:MaxUploadBytes") ?? 100 * 1024 * 1024;
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = uploadLimit + 1024 * 1024);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = uploadLimit + 1024 * 1024);
builder.Services.AddScoped<ICurriculumService, CurriculumService>();
builder.Services.AddScoped<MwabuLearn.Application.Directories.IDirectoryService, MwabuLearn.Infrastructure.Directories.DirectoryService>();
builder.Services.AddMwabuAuthentication(builder.Configuration);
builder.Services.AddOptions<MwabuLearn.Infrastructure.Devices.DeviceOptions>().BindConfiguration("Devices")
    .Validate(MwabuLearn.Infrastructure.Devices.DeviceOptions.IsValid, "Invalid device credential lifetime.").ValidateOnStart();
builder.Services.AddOptions<MwabuLearn.Infrastructure.Sync.SyncOptions>().BindConfiguration("Sync")
    .Validate(MwabuLearn.Infrastructure.Sync.SyncOptions.IsValid, "Invalid sync bounds.").ValidateOnStart();
builder.Services.AddScoped<MwabuLearn.Infrastructure.Sync.SyncCursorProtector>();
builder.Services.AddScoped<MwabuLearn.Application.Sync.ISyncService, MwabuLearn.Infrastructure.Sync.SyncService>();
builder.Services.AddScoped<MwabuLearn.Application.Devices.IDeviceService, MwabuLearn.Infrastructure.Devices.DeviceService>();
builder.Services.AddScoped<MwabuLearn.Application.Devices.IDeviceContext, HttpDeviceContext>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, RegisteredDeviceHandler>();
builder.Services.AddAuthorization(options => options.AddPolicy("registered-device", policy => policy.RequireAuthenticatedUser().AddRequirements(new RegisteredDeviceRequirement())));
builder.Services.AddScoped<MwabuLearn.Application.Auditing.IAuditContext, HttpAuditContext>();
builder.Services.AddScoped<MwabuLearn.Application.Auditing.IAuditService, MwabuLearn.Infrastructure.Auditing.AuditService>();
builder.AddHttpSecurity();
builder.AddBackendObservability();
builder.AddDurableDataProtection();
builder.Logging.ClearProviders();
// Framework database/client diagnostics may include SQL, URLs or provider exception details.
// SafeExceptionHandler and operational spans supply sanitized failure diagnostics.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Critical);
builder.Logging.AddFilter("Npgsql", LogLevel.Critical);
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
    context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.Items["CorrelationId"];
});
builder.Services.AddExceptionHandler<CurriculumExceptionHandler>();
builder.Services.AddExceptionHandler<ContentExceptionHandler>();
builder.Services.AddExceptionHandler<IdentityExceptionHandler>();
builder.Services.AddExceptionHandler<SafeExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", Description = "Enter an access token obtained from /api/auth/login." });
    options.OperationFilter<BearerOperationFilter>();
    options.SwaggerDoc("v1", new()
    {
        Title = "Mwabu Learn API",
        Version = "v1",
        Description = "Backend API for the Mwabu Learn v2 platform."
    });
});

builder.Services.AddMwabuDatabase(builder.Configuration, builder.Environment.IsProduction());

var app = builder.Build();
app.UseForwardedHeaders();
app.UseMiddleware<RequestTelemetryMiddleware>();
app.UseExceptionHandler(new ExceptionHandlerOptions { SuppressDiagnosticsCallback = _ => true });
if (!app.Environment.IsDevelopment()) app.UseHsts();

// -------------------------------------------------------
// HTTP pipeline
// -------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "Mwabu Learn API v1");

        options.RoutePrefix = "swagger";
    });
}

app.UseWhen(context => !context.Request.Path.StartsWithSegments("/health"), branch => branch.UseHttpsRedirection());

app.UseCors("ConfiguredOrigins");

app.UseAuthentication();
app.UseMiddleware<AuthenticationPartitionMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

// -------------------------------------------------------
// Health endpoint
// -------------------------------------------------------

app.MapOperationalHealth();

app.Run();

public partial class Program;
