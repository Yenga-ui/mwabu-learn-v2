
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
var builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------
// Services
// -------------------------------------------------------

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddScoped<IContentService, ContentService>();
builder.Services.AddSingleton<IContentStorage, LocalContentStorage>();
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
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<CurriculumExceptionHandler>();
builder.Services.AddExceptionHandler<ContentExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Mwabu Learn API",
        Version = "v1",
        Description = "Backend API for the Mwabu Learn v2 platform."
    });
});

var connectionString =
    builder.Configuration.GetConnectionString("MwabuLearnDb")
    ?? throw new InvalidOperationException(
        "Connection string 'MwabuLearnDb' was not found.");

builder.Services.AddDbContext<MwabuDbContext>(options =>
    options.UseNpgsql(connectionString));
// CORS will allow our React and Flutter clients to access the API.
// We will tighten this policy for production.
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentCors", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();
app.UseExceptionHandler();

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

app.UseHttpsRedirection();

app.UseCors("DevelopmentCors");

app.UseAuthorization();

app.MapControllers();

// -------------------------------------------------------
// Health endpoint
// -------------------------------------------------------

app.MapGet("/api/health", () =>
{
    return Results.Ok(new
    {
        status = "healthy",
        application = "Mwabu Learn API",
        version = "2.0.0",
        environment = app.Environment.EnvironmentName,
        timestamp = DateTime.UtcNow
    });
})
.WithName("HealthCheck")
.WithTags("System");

app.Run();

public partial class Program;
