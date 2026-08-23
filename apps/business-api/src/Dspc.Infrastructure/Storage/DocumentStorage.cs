using Amazon.S3;
using Amazon.S3.Model;
using Dspc.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Dspc.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string Section = "Storage";
    public string Provider { get; set; } = "FileSystem";
    public string Root { get; set; } = "./storage";
    public MinioOptions Minio { get; set; } = new();
}

public sealed class MinioOptions
{
    public string Endpoint { get; set; } = "http://localhost:9000";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Bucket { get; set; } = "dspc-documents";
}

internal static class StorageKeys
{
    public static string Sanitize(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Empty storage key");
        var parts = key.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(p => p is "." or ".." || p.Contains(':'))) throw new ArgumentException("Unsafe storage key");
        return string.Join('/', parts);
    }
}

public sealed class FileSystemDocumentStorage(IOptions<StorageOptions> options) : IDocumentStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.Root);
    public string Provider => "FileSystem";

    private string PathFor(string key)
    {
        var full = Path.GetFullPath(Path.Combine(_root, StorageKeys.Sanitize(key)));
        if (!full.StartsWith(_root, StringComparison.Ordinal)) throw new ArgumentException("Path traversal detected");
        return full;
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var fs = File.Create(path);
        await content.CopyToAsync(fs, ct);
    }

    public Task<Stream?> GetAsync(string key, CancellationToken ct)
    {
        var path = PathFor(key);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct) => Task.FromResult(File.Exists(PathFor(key)));
    public Task DeleteAsync(string key, CancellationToken ct) { var p = PathFor(key); if (File.Exists(p)) File.Delete(p); return Task.CompletedTask; }
    public Task<bool> HealthCheckAsync(CancellationToken ct) { Directory.CreateDirectory(_root); return Task.FromResult(Directory.Exists(_root)); }
}

public sealed class MinioDocumentStorage : IDocumentStorage
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private bool _bucketEnsured;
    public string Provider => "Minio";

    public MinioDocumentStorage(IOptions<StorageOptions> options)
    {
        var m = options.Value.Minio;
        _bucket = m.Bucket;
        _s3 = new AmazonS3Client(m.AccessKey, m.SecretKey, new AmazonS3Config { ServiceURL = m.Endpoint, ForcePathStyle = true, AuthenticationRegion = "us-east-1", UseHttp = m.Endpoint.StartsWith("http://") });
    }

    private async Task EnsureBucketAsync(CancellationToken ct)
    {
        if (_bucketEnsured) return;
        var exists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3, _bucket);
        if (!exists) await _s3.PutBucketAsync(new PutBucketRequest { BucketName = _bucket }, ct);
        _bucketEnsured = true;
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        await EnsureBucketAsync(ct);
        await _s3.PutObjectAsync(new PutObjectRequest { BucketName = _bucket, Key = StorageKeys.Sanitize(key), InputStream = content, ContentType = contentType, AutoCloseStream = false }, ct);
    }

    public async Task<Stream?> GetAsync(string key, CancellationToken ct)
    {
        await EnsureBucketAsync(ct);
        try
        {
            var resp = await _s3.GetObjectAsync(_bucket, StorageKeys.Sanitize(key), ct);
            var ms = new MemoryStream();
            await resp.ResponseStream.CopyToAsync(ms, ct);
            ms.Position = 0;
            return ms;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { return null; }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct)
    {
        await EnsureBucketAsync(ct);
        try { await _s3.GetObjectMetadataAsync(_bucket, StorageKeys.Sanitize(key), ct); return true; }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { return false; }
    }

    public async Task DeleteAsync(string key, CancellationToken ct) { await EnsureBucketAsync(ct); await _s3.DeleteObjectAsync(_bucket, StorageKeys.Sanitize(key), ct); }

    public async Task<bool> HealthCheckAsync(CancellationToken ct)
    {
        try { await EnsureBucketAsync(ct); return true; } catch { return false; }
    }
}
