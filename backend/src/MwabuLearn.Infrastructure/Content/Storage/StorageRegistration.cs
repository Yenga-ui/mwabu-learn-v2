using Amazon;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Content;
namespace MwabuLearn.Infrastructure.Content.Storage;

public static class StorageRegistration
{
    public static void AddContentStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["ContentStorage:Provider"] ?? "Local";
        if (provider == "Local") services.AddSingleton<IContentStorage, LocalContentStorage>();
        else if (provider == "S3")
        {
            services.AddOptions<S3StorageOptions>().BindConfiguration("ContentStorage:S3")
                .Validate(S3StorageOptions.IsValid, "Invalid S3 settings; require a bucket, region and HTTPS endpoint.").ValidateOnStart();
            services.AddSingleton<IAmazonS3>(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<S3StorageOptions>>().Value;
                var config = new AmazonS3Config { RegionEndpoint = RegionEndpoint.GetBySystemName(settings.Region), ForcePathStyle = settings.ForcePathStyle, MaxErrorRetry = 0 };
                if (settings.ServiceUrl is not null) { config.ServiceURL = settings.ServiceUrl; config.AuthenticationRegion = settings.Region; }
                // AWS credential provider chain: environment, workload identity or managed role. No credentials in options/source.
                return new AmazonS3Client(config);
            });
            services.AddSingleton<IContentStorage, S3ContentStorage>();
        }
        else throw new InvalidOperationException("ContentStorage:Provider must be Local or S3.");
        services.AddSingleton<IStorageReadiness>(sp => (IStorageReadiness)sp.GetRequiredService<IContentStorage>());
    }
}
