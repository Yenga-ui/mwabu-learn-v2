using System.Net;
using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Content;
using MwabuLearn.Infrastructure.Content.Storage;
namespace MwabuLearn.Tests;

public sealed class S3StorageTests
{
    private static string Key() => $"content/{Guid.NewGuid():N}/assets/{Guid.NewGuid():N}";
    private sealed class Fixture : IDisposable
    {
        private readonly string root = Directory.CreateTempSubdirectory("mwabu-content-tests-").FullName;
        public FakeS3 Client { get; } = new();
        public S3ContentStorage Storage { get; }
        public Fixture() => Storage = new(Client, Options.Create(new S3StorageOptions { Bucket = "test-bucket", Region = "us-east-1", SpoolDirectory = root }));
        public void AssertNoSpool() => Assert.Empty(Directory.EnumerateFiles(root));
        public void Dispose() { Client.Dispose(); ContentTestEnvironment.DeleteTestRoot(root); }
    }
    private sealed class FakeS3 : AmazonS3Client
    {
        public Dictionary<string, byte[]> Objects { get; } = [];
        public List<(long Start, long End)> Ranges { get; } = [];
        public bool FailUpload { get; set; }
        public FakeS3() : base(new AnonymousAWSCredentials(), new AmazonS3Config { RegionEndpoint = Amazon.RegionEndpoint.USEast1 }) { }
        public override async Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken ct = default)
        {
            Assert.Equal("*", request.IfNoneMatch);
            if (FailUpload) throw new IOException("Simulated provider failure");
            if (Objects.ContainsKey(request.Key)) throw new AmazonS3Exception("Conflict") { StatusCode = HttpStatusCode.PreconditionFailed };
            using var destination = new MemoryStream();
            await request.InputStream.CopyToAsync(destination, ct);
            var bytes = destination.ToArray();
            Assert.Equal(Convert.ToBase64String(SHA256.HashData(bytes)), request.ChecksumSHA256);
            Objects.Add(request.Key, bytes);
            return new();
        }
        public override Task<GetObjectMetadataResponse> GetObjectMetadataAsync(GetObjectMetadataRequest request, CancellationToken ct = default) =>
            Objects.TryGetValue(request.Key, out var bytes) ? Task.FromResult(new GetObjectMetadataResponse { ContentLength = bytes.Length }) :
            throw new AmazonS3Exception("Missing") { StatusCode = HttpStatusCode.NotFound };
        public override Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request, CancellationToken ct = default)
        {
            Ranges.Add((request.ByteRange.Start, request.ByteRange.End));
            return Task.FromResult(new GetObjectResponse { ResponseStream = new MemoryStream(Objects[request.Key][(int)request.ByteRange.Start..((int)request.ByteRange.End + 1)]) });
        }
        public override Task<DeleteObjectResponse> DeleteObjectAsync(DeleteObjectRequest request, CancellationToken ct = default)
        { Objects.Remove(request.Key); return Task.FromResult(new DeleteObjectResponse()); }
    }
    [Fact]
    public async Task Upload_hashes_observed_bytes_refuses_overwrite_and_removes_spool()
    {
        using var fixture = new Fixture(); var key = Key(); var bytes = RandomNumberGenerator.GetBytes(10000);
        var stored = await fixture.Storage.StoreAsync(key, new MemoryStream(bytes), bytes.Length, default);
        Assert.Equal(bytes.Length, stored.FileSizeBytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), stored.Checksum);
        Assert.Equal(bytes, fixture.Client.Objects[key]);
        await Assert.ThrowsAsync<ContentException>(() => fixture.Storage.StoreAsync(key, new MemoryStream(new byte[] { 1 }), 10, default));
        Assert.Equal(bytes, fixture.Client.Objects[key]); fixture.AssertNoSpool();
    }
    [Fact]
    public async Task Oversize_empty_and_failed_uploads_do_not_leave_partial_objects_or_spools()
    {
        using var fixture = new Fixture();
        await Assert.ThrowsAsync<ContentException>(() => fixture.Storage.StoreAsync(Key(), new MemoryStream(new byte[11]), 10, default));
        await Assert.ThrowsAsync<ContentException>(() => fixture.Storage.StoreAsync(Key(), new MemoryStream(), 10, default));
        fixture.Client.FailUpload = true;
        await Assert.ThrowsAsync<IOException>(() => fixture.Storage.StoreAsync(Key(), new MemoryStream(new byte[1]), 10, default));
        Assert.Empty(fixture.Client.Objects); fixture.AssertNoSpool();
    }
    [Fact]
    public async Task Seek_uses_remote_range_and_deletion_is_idempotent()
    {
        using var fixture = new Fixture(); var key = Key();
        await fixture.Storage.StoreAsync(key, new MemoryStream(Enumerable.Range(0, 100).Select(x => (byte)x).ToArray()), 100, default);
        await using(var stream = await fixture.Storage.OpenReadAsync(key, default))
        {
            stream.Seek(70, SeekOrigin.Begin);
            var bytes = new byte[5];
            Assert.Equal(5, await stream.ReadAsync(bytes)); Assert.Equal(new byte[] { 70, 71, 72, 73, 74 }, bytes);
            Assert.Equal((70L, 99L), Assert.Single(fixture.Client.Ranges));
            stream.Seek(10, SeekOrigin.Begin); Assert.Equal(5, await stream.ReadAsync(bytes));
            Assert.Equal(10, bytes[0]); Assert.Equal(2, fixture.Client.Ranges.Count);
        }
        await fixture.Storage.DeleteAsync(key, default); await fixture.Storage.DeleteAsync(key, default);
        Assert.False(await fixture.Storage.ExistsAsync(key, default));
    }
}
