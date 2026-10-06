using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Api.Operations;

public sealed class ProtectionCertificateOptions
{
    public string Path { get; set; } = "";
    public string Password { get; set; } = "";
}
public sealed class ProtectionOptions
{
    public bool PersistKeysInDatabase { get; set; }
    public ProtectionCertificateOptions Certificate { get; set; } = new();
    public ProtectionCertificateOptions[] PreviousCertificates { get; set; } = [];
}
public static class ProtectionRegistration
{
    public static void AddDurableDataProtection(this WebApplicationBuilder builder)
    {
        var settings = builder.Configuration.GetSection("DataProtection").Get<ProtectionOptions>() ?? new();
        if (builder.Environment.IsProduction() && (!settings.PersistKeysInDatabase || string.IsNullOrWhiteSpace(settings.Certificate.Path)))
            throw new InvalidOperationException("Production requires durable database Data Protection keys and an operator-supplied encryption certificate.");
        var protection = builder.Services.AddDataProtection().SetApplicationName("MwabuLearn.Api.v1");
        if (settings.PersistKeysInDatabase) protection.PersistKeysToDbContext<MwabuDbContext>();
        if (string.IsNullOrWhiteSpace(settings.Certificate.Path)) return;
        var certificates = new List<X509Certificate2>();
        X509Certificate2 Load(ProtectionCertificateOptions options, bool active)
        {
            if (string.IsNullOrWhiteSpace(options.Path) || options.Password.Length < 16)
                throw new InvalidOperationException("Data Protection certificate path and a strong secret password are required.");
            X509Certificate2 certificate;
            try { certificate = X509CertificateLoader.LoadPkcs12FromFile(options.Path, options.Password, X509KeyStorageFlags.EphemeralKeySet); }
            catch (Exception ex) when (ex is CryptographicException or IOException)
            { throw new InvalidOperationException("Unable to load the Data Protection certificate; verify the mounted secret and permissions."); }
            using var rsa = certificate.GetRSAPublicKey();
            if (!certificate.HasPrivateKey || rsa is null || rsa.KeySize < 2048 || active && (certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow))
            { certificate.Dispose(); throw new InvalidOperationException("Data Protection requires a valid RSA encryption certificate with private key."); }
            certificates.Add(certificate); return certificate;
        }
        try
        {
            var active = Load(settings.Certificate, true);
            protection.ProtectKeysWithCertificate(active);
            foreach (var previous in settings.PreviousCertificates) Load(previous, false);
            protection.UnprotectKeysWithAnyCertificate(certificates.ToArray());
            builder.Services.AddSingleton<IHostedService>(_ => new CertificateLifetime(certificates));
        }
        catch { foreach (var certificate in certificates) certificate.Dispose(); throw; }
    }
    private sealed class CertificateLifetime(IReadOnlyList<X509Certificate2> certificates) : IHostedService, IDisposable
    {
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
        public void Dispose() { foreach (var certificate in certificates) certificate.Dispose(); }
    }
}
