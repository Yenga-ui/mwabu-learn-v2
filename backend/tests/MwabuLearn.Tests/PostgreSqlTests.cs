using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Domain.Entities.Organisations;
using MwabuLearn.Infrastructure.Identity;
using MwabuLearn.Infrastructure.Persistence;
using Npgsql;
namespace MwabuLearn.Tests;

public sealed class PostgreSqlTests
{
    [PostgreSqlFact]
    public async Task All_migrations_apply_and_unique_identity_indexes_and_audit_trigger_are_real()
    {
        await using var fixture = await PostgreSqlFixture.CreateAsync(); await using var db = fixture.Context();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(9, await db.OrganisationRoles.CountAsync()); Assert.Equal(13, await db.Permissions.CountAsync());
        var user = new ApplicationUser { Email = "unique@example.test", NormalizedEmail = "UNIQUE@EXAMPLE.TEST", UserName = "unique@example.test", NormalizedUserName = "UNIQUE@EXAMPLE.TEST", FirstName = "Test", LastName = "User" };
        db.Users.Add(user); await db.SaveChangesAsync();
        db.Users.Add(new ApplicationUser { Email = "unique@example.test", NormalizedEmail = user.NormalizedEmail, UserName = "another@example.test", NormalizedUserName = "ANOTHER@EXAMPLE.TEST", FirstName = "Test", LastName = "User" });
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
        db.ChangeTracker.Clear();
        var blocked = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"AuditEvents\" SET \"EventType\" = 'tampered'"));
        Assert.Equal(PostgresErrorCodes.RaiseException, blocked.SqlState);
    }
    [PostgreSqlFact]
    public async Task Filtered_primary_asset_index_and_mapping_check_constraint_are_enforced()
    {
        await using var fixture = await PostgreSqlFixture.CreateAsync(); await using var db = fixture.Context();
        var content = new ContentItem { Title = "Constraint test", Slug = "constraint-test", ContentType = "lesson", LanguageCode = "en" };
        db.ContentItems.Add(content); await db.SaveChangesAsync();
        for (var i = 0; i < 2; i++)
        {
            var asset = new ContentAsset { ContentItemId = content.Id, FileName = "lesson.pdf", MimeType = "application/pdf", AssetType = AssetType.Document, FileSizeBytes = 1, Checksum = new string('a', 64), IsPrimary = true };
            asset.StorageKey = $"content/{content.Id:N}/assets/{asset.Id:N}"; db.ContentAssets.Add(asset);
        }
        var primary = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(primary.InnerException).SqlState); db.ChangeTracker.Clear();
        Assert.Equal(0, await db.ContentAssets.CountAsync());
        db.ContentCurriculumMappings.Add(new ContentCurriculumMapping { ContentItemId = content.Id });
        var mapping = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(mapping.InnerException).SqlState);
    }
    [PostgreSqlFact]
    public async Task Catalogue_version_allocation_cannot_overtake_an_uncommitted_writer()
    {
        await using var fixture = await PostgreSqlFixture.CreateAsync(); await using var first = fixture.Context(); await using var second = fixture.Context();
        await using var observer = fixture.Context(); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var tx = await first.Database.BeginTransactionAsync(timeout.Token);
        var a = new Curriculum { Name = "First writer", CountryCode = "ZM" }; first.Curricula.Add(a); await first.SaveChangesAsync(timeout.Token);
        var b = new Curriculum { Name = "Second writer", CountryCode = "ZM" }; second.Curricula.Add(b);
        var save = second.SaveChangesAsync(timeout.Token);
        Assert.NotSame(save, await Task.WhenAny(save, Task.Delay(200, timeout.Token)));
        Assert.Equal(0, await observer.SyncClock.Select(x => x.Version).SingleAsync(timeout.Token));
        Assert.Empty(await observer.SyncChanges.ToListAsync(timeout.Token));
        await tx.CommitAsync(timeout.Token); await save;
        var changes = await observer.SyncChanges.OrderBy(x => x.Version).ToListAsync(timeout.Token);
        Assert.Equal(new[] { a.Id, b.Id }, changes.Select(x => x.EntityId)); Assert.Equal(new long[] { 1, 2 }, changes.Select(x => x.Version));
        Assert.Equal(2, await observer.SyncClock.Select(x => x.Version).SingleAsync(timeout.Token));
        // Exercises provider GUID keyset comparison used by bootstrap pagination.
        var low = await observer.Curricula.OrderBy(x => x.Id).Select(x => x.Id).FirstAsync(timeout.Token);
        Assert.Single(await observer.Curricula.Where(x => x.Id.CompareTo(low) > 0).ToListAsync(timeout.Token));
    }
    [PostgreSqlFact]
    public async Task Restrictive_membership_foreign_keys_and_unique_membership_are_enforced()
    {
        await using var fixture = await PostgreSqlFixture.CreateAsync(); await using var db = fixture.Context();
        var user = new ApplicationUser { Email = "member@example.test", NormalizedEmail = "MEMBER@EXAMPLE.TEST", UserName = "member@example.test", NormalizedUserName = "MEMBER@EXAMPLE.TEST", FirstName = "Test", LastName = "User" };
        var organisation = new Organisation { Name = "Isolated school", Code = "ISOLATED", OrganisationType = OrganisationType.School };
        db.Users.Add(user); db.Organisations.Add(organisation); await db.SaveChangesAsync();
        db.OrganisationMemberships.Add(new() { UserId = user.Id, OrganisationId = organisation.Id }); await db.SaveChangesAsync();
        db.OrganisationMemberships.Add(new() { UserId = user.Id, OrganisationId = organisation.Id });
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState); db.ChangeTracker.Clear();
        var restricted = await Assert.ThrowsAsync<PostgresException>(() => db.Organisations.Where(x => x.Id == organisation.Id).ExecuteDeleteAsync());
        await db.Database.OpenConnectionAsync();
        var major = ((NpgsqlConnection)db.Database.GetDbConnection()).PostgreSqlVersion.Major;
        // PostgreSQL 18 reports ON DELETE RESTRICT as 23001; PostgreSQL 17 uses 23503.
        Assert.Equal(major >= 18 ? PostgresErrorCodes.RestrictViolation : PostgresErrorCodes.ForeignKeyViolation, restricted.SqlState);
        Assert.True(await db.Organisations.AnyAsync(x => x.Id == organisation.Id));
        Assert.Equal(1, await db.OrganisationMemberships.CountAsync(x => x.OrganisationId == organisation.Id));
    }
    [PostgreSqlFact]
    public async Task Identity_login_me_and_concurrent_refresh_rotation_work_on_postgresql()
    {
        await using var fixture = await PostgreSqlFixture.CreateAsync(); var race = new RefreshRaceInterceptor(); using var factory = new PgApiFactory(fixture.ConnectionString, race);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var password = TestSecurityConfiguration.StrongPassword(); const string email = "admin@integration.test";
        using (var scope = factory.Services.CreateScope())
            await new BootstrapAdministrator(scope.ServiceProvider.GetRequiredService<MwabuDbContext>(), scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
                Options.Create(new BootstrapOptions { Enabled = true, Email = email, Password = password, FirstName = "Initial", LastName = "Admin", OrganisationName = "Platform", OrganisationCode = "PG-PLATFORM" })).InitializeAsync(default);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password }); Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = (await login.Content.ReadFromJsonAsync<LoginResponse>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null; // Refresh must exercise rotation, not an expired bearer challenge.
        var attempts = await Task.WhenAll(client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(session.RefreshToken!)), client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(session.RefreshToken!)));
        Assert.Single(attempts, x => x.StatusCode == HttpStatusCode.OK);
        Assert.All(attempts, x => Assert.True(x.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized or HttpStatusCode.Conflict,
            $"Unexpected {x.StatusCode}; PostgreSQL save failures: {string.Join("; ", race.Failures)}"));
        var winner = (await attempts.Single(x => x.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", winner.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Contains(race.Failures, failure => failure.Contains("PostgresException SQLSTATE=40001", StringComparison.Ordinal));
        using (var scope = factory.Services.CreateScope())
        {
            var rows = await scope.ServiceProvider.GetRequiredService<MwabuDbContext>().RefreshSessions.AsNoTracking().ToListAsync();
            Assert.Equal(2, rows.Count); // Exactly original + one committed replacement, no losing insert.
            var original = Assert.Single(rows, x => x.ReplacedBySessionId != null);
            var replacement = Assert.Single(rows, x => x.RevokedAt == null);
            Assert.Equal(replacement.Id, original.ReplacedBySessionId);
            Assert.Equal(original.FamilyId, replacement.FamilyId);
        }
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(session.RefreshToken!))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(winner.RefreshToken!))).StatusCode);
        using (var scope = factory.Services.CreateScope())
            Assert.All(await scope.ServiceProvider.GetRequiredService<MwabuDbContext>().RefreshSessions.AsNoTracking().ToListAsync(), x => Assert.NotNull(x.RevokedAt));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", winner.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        foreach (var attempt in attempts) attempt.Dispose();
    }
    [PostgreSqlFact]
    public async Task Sequential_refresh_logout_logout_all_and_password_change_preserve_session_security()
    {
        await using var fixture = await PostgreSqlFixture.CreateAsync(); using var factory = new PgApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var password = TestSecurityConfiguration.StrongPassword(); UserResponse user;
        using (var scope = factory.Services.CreateScope())
            user = await scope.ServiceProvider.GetRequiredService<IUserService>().CreateAsync(new CreateUserRequest
            { Email = "sessions@integration.test", FirstName = "Session", LastName = "Test", InitialPassword = password }, default);
        async Task<LoginResponse> Login(string secret)
        {
            client.DefaultRequestHeaders.Authorization = null;
            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = user.Email, Password = secret });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        }
        async Task<HttpResponseMessage> Refresh(LoginResponse session)
        {
            client.DefaultRequestHeaders.Authorization = null;
            return await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(session.RefreshToken!));
        }
        void Authorize(LoginResponse session) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var initial = await Login(password);
        var refreshed = await Refresh(initial); Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var next = (await refreshed.Content.ReadFromJsonAsync<LoginResponse>())!;
        var sequential = await Refresh(next); Assert.Equal(HttpStatusCode.OK, sequential.StatusCode);
        var latest = (await sequential.Content.ReadFromJsonAsync<LoginResponse>())!;
        Authorize(latest);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/logout", new RefreshRequest(latest.RefreshToken!))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(latest)).StatusCode);
        var first = await Login(password); var second = await Login(password); Authorize(first);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout-all", null)).StatusCode);
        Authorize(second); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(first)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(second)).StatusCode);
        first = await Login(password); second = await Login(password); Authorize(first);
        var newPassword = TestSecurityConfiguration.StrongPassword();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest(password, newPassword))).StatusCode);
        Authorize(second); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(first)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(second)).StatusCode);
        Assert.NotNull((await Login(newPassword)).RefreshToken);
    }
    private sealed class PgApiFactory(string connection, Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor? interceptor = null) : WebApplicationFactory<Program>
    {
        private readonly string root = Directory.CreateTempSubdirectory("mwabu-content-tests-").FullName;
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            TestSecurityConfiguration.Configure(builder); builder.UseEnvironment("Testing"); builder.UseSetting("ConnectionStrings:MwabuLearnDb", connection);
            builder.UseSetting("ContentStorage:RootPath", root);
            if (interceptor is not null) builder.ConfigureServices(services => services.AddDbContext<MwabuDbContext>((_, options) => options.AddInterceptors(interceptor)));
        }
        protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) ContentTestEnvironment.DeleteTestRoot(root); }
    }
}
