using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Organisations;
using MwabuLearn.Infrastructure.Identity;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Tests;

internal sealed class IdentityTestEnvironment : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly IServiceScope inspection;
    public ServiceProvider Provider { get; }
    public MwabuDbContext Db => inspection.ServiceProvider.GetRequiredService<MwabuDbContext>();
    public TestActor Actor { get; } = new();
    public string AdminEmail { get; } = "bootstrap@identity.test";
    public string AdminPassword { get; } = TestSecurityConfiguration.StrongPassword();
    public BootstrapOptions Bootstrap => new()
    {
        Enabled = true, Email = AdminEmail, Password = AdminPassword, FirstName = "Initial", LastName = "Administrator",
        OrganisationName = "Platform boundary", OrganisationCode = "PLATFORM"
    };
    public IdentityTestEnvironment()
    {
        connection.Open();
        var services = new ServiceCollection();
        services.AddLogging(); services.AddDataProtection(); services.AddAuthentication(); services.AddHttpContextAccessor();
        services.AddSingleton<ICurrentUser>(Actor);
        services.AddDbContext<MwabuDbContext>(o => o.UseSqlite(connection));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "https://identity.mwabu.test", ["Jwt:Audience"] = "mwabu-test-api",
            ["Jwt:SigningKeyBase64"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ["BootstrapAdministrator:Enabled"] = "false"
        }).Build();
        services.AddMwabuIdentity(config);
        Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        inspection = Provider.CreateScope();
        Db.Database.EnsureCreated();
    }
    public async Task<T> Run<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = Provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        return await action(scope.ServiceProvider);
    }
    public async Task Run(Func<IServiceProvider, Task> action)
    {
        using var scope = Provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        await action(scope.ServiceProvider);
    }
    public async Task BootstrapAsync(BootstrapOptions? options = null)
    {
        await Run(sp => new BootstrapAdministrator(sp.GetRequiredService<MwabuDbContext>(), sp.GetRequiredService<UserManager<ApplicationUser>>(),
            Options.Create(options ?? Bootstrap)).InitializeAsync(CancellationToken.None));
        Actor.UserId = await Db.Users.AsNoTracking().Where(x => x.NormalizedEmail == AdminEmail.ToUpperInvariant()).Select(x => (Guid?)x.Id).SingleOrDefaultAsync();
    }
    public Task<UserResponse> CreateUser(string email = "teacher@identity.test", string? password = null) => Run(sp => sp.GetRequiredService<IUserService>().CreateAsync(
        new CreateUserRequest { Email = email, FirstName = "First", LastName = "Last", InitialPassword = password ?? TestSecurityConfiguration.StrongPassword() }, CancellationToken.None));
    public Task<OrganisationResponse> CreateOrganisation(string code = "SCHOOL", Guid? parent = null, OrganisationType type = OrganisationType.School) =>
        Run(sp => sp.GetRequiredService<IOrganisationService>().CreateAsync(new OrganisationRequest { Name = code, Code = code, OrganisationType = type, ParentOrganisationId = parent }, CancellationToken.None));
    public Task<bool> Can(Guid user, string code, Guid? organisation = null, bool platformOnly = false) =>
        Run(sp => sp.GetRequiredService<IPermissionEvaluator>().CanAsync(user, code, organisation, platformOnly, CancellationToken.None));
    public async Task<Guid> RoleId(string code) => await Db.OrganisationRoles.AsNoTracking().Where(x => x.Code == code).Select(x => x.Id).SingleAsync();
    public void Dispose() { inspection.Dispose(); Provider.Dispose(); connection.Dispose(); }
    internal sealed class TestActor : ICurrentUser { public Guid? UserId { get; set; } }
}
