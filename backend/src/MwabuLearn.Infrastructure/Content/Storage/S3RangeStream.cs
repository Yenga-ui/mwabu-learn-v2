using Amazon.S3;
using Amazon.S3.Model;
namespace MwabuLearn.Infrastructure.Content.Storage;

// Seeking translates into a new provider Range request. No download-to-disk/RAM emulation.
internal sealed class S3RangeStream(IAmazonS3 client, string bucket, string key, long length) : Stream
{
    private long position;
    private GetObjectResponse? response;
    private bool disposed;
    public override bool CanRead => !disposed;
    public override bool CanSeek => !disposed;
    public override bool CanWrite => false;
    public override long Length => length;
    public override long Position { get => position; set => Seek(value, SeekOrigin.Begin); }
    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var next = checked(origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => position + offset, SeekOrigin.End => length + offset, _ => throw new ArgumentOutOfRangeException(nameof(origin)) });
        if (next < 0) throw new IOException("Cannot seek before the beginning.");
        if (next != position) { response?.Dispose(); response = null; position = next; }
        return position;
    }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (position >= length || buffer.Length == 0) return 0;
        response ??= await client.GetObjectAsync(new GetObjectRequest { BucketName = bucket, Key = key, ByteRange = new ByteRange(position, length - 1) }, ct);
        var read = await response.ResponseStream.ReadAsync(buffer[..(int)Math.Min(buffer.Length, length - position)], ct);
        position += read;
        if (read == 0 && position < length) throw new EndOfStreamException("Object response ended before its declared length.");
        return read;
    }
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Use asynchronous reads.");
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { if (disposing) { response?.Dispose(); response = null; disposed = true; } base.Dispose(disposing); }
}
