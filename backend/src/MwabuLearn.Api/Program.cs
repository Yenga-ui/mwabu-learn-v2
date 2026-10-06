
using Microsoft.EntityFrameworkCore;
using MwabuLearn.Infrastructure.Persistence;
var builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------
// Services
// -------------------------------------------------------

builder.Services.AddControllers();

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