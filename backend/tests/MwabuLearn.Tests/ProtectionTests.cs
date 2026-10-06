using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Tests;

public sealed class ProtectionTests
{
    [Fact]
    public async Task Database_key_ring_is_encrypted_and_an_independent_provider_can_decrypt_after_restart()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=mwabu-isolated-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        ServiceProvider Provider()
        {
            var services = new ServiceCollection(); services.AddLogging();
            services.AddDbContext<MwabuDbContext>(o => o.UseSqlite(connection));
            services.AddDataProtection().SetApplicationName("mwabu-isolated-protection-test").PersistKeysToDbContext<MwabuDbContext>()
                .ProtectKeysWithCertificate(certificate).UnprotectKeysWithAnyCertificate(certificate);
            return services.BuildServiceProvider();
        }
        string protectedValue;
        using (var first = Provider())
        {
            using var scope = first.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MwabuDbContext>();
            await db.Database.EnsureCreatedAsync();
            protectedValue = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("cursor-test").Protect("opaque-cursor-state");
            var key = await db.DataProtectionKeys.AsNoTracking().SingleAsync();
            Assert.Contains("encryptedSecret", key.Xml!); Assert.DoesNotContain("opaque-cursor-state", key.Xml!);
        }
        using var second = Provider();
        Assert.Equal("opaque-cursor-state", second.GetRequiredService<IDataProtectionProvider>().CreateProtector("cursor-test").Unprotect(protectedValue));
        Assert.Throws<CryptographicException>(() => second.GetRequiredService<IDataProtectionProvider>().CreateProtector("wrong-purpose").Unprotect(protectedValue));
    }
}
