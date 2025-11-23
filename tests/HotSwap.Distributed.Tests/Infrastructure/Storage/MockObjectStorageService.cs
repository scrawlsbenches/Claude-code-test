using HotSwap.Distributed.Infrastructure.Storage;
using System.Collections.Concurrent;

namespace HotSwap.Distributed.Tests.Infrastructure.Storage;

/// <summary>
/// Mock implementation of IObjectStorageService for testing.
/// </summary>
public class MockObjectStorageService : IObjectStorageService
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte[]>> _buckets = new();
    private bool _isHealthy = true;

    public Task<bool> CreateBucketAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        var created = _buckets.TryAdd(bucketName, new ConcurrentDictionary<string, byte[]>());
        return Task.FromResult(created);
    }

    public Task DeleteBucketAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        _buckets.TryRemove(bucketName, out _);
        return Task.CompletedTask;
    }

    public Task<bool> BucketExistsAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_buckets.ContainsKey(bucketName));
    }

    public Task<string> UploadObjectAsync(
        string bucketName,
        string objectName,
        Stream data,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (!_buckets.TryGetValue(bucketName, out var bucket))
        {
            throw new InvalidOperationException($"Bucket {bucketName} does not exist");
        }

        using var memoryStream = new MemoryStream();
        data.CopyTo(memoryStream);
        var bytes = memoryStream.ToArray();

        bucket[objectName] = bytes;

        return Task.FromResult($"https://mock-storage/{bucketName}/{objectName}");
    }

    public Task<Stream> DownloadObjectAsync(
        string bucketName,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        if (!_buckets.TryGetValue(bucketName, out var bucket))
        {
            throw new InvalidOperationException($"Bucket {bucketName} does not exist");
        }

        if (!bucket.TryGetValue(objectName, out var bytes))
        {
            throw new KeyNotFoundException($"Object {objectName} not found in bucket {bucketName}");
        }

        return Task.FromResult<Stream>(new MemoryStream(bytes));
    }

    public Task DeleteObjectAsync(
        string bucketName,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        if (_buckets.TryGetValue(bucketName, out var bucket))
        {
            bucket.TryRemove(objectName, out _);
        }

        return Task.CompletedTask;
    }

    public Task<long> GetBucketSizeAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        if (!_buckets.TryGetValue(bucketName, out var bucket))
        {
            return Task.FromResult(0L);
        }

        var totalSize = bucket.Values.Sum(bytes => (long)bytes.Length);
        return Task.FromResult(totalSize);
    }

    public Task<int> GetObjectCountAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        if (!_buckets.TryGetValue(bucketName, out var bucket))
        {
            return Task.FromResult(0);
        }

        return Task.FromResult(bucket.Count);
    }

    public Task<IReadOnlyList<string>> ListObjectsAsync(
        string bucketName,
        string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        if (!_buckets.TryGetValue(bucketName, out var bucket))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var objects = bucket.Keys
            .Where(key => string.IsNullOrEmpty(prefix) || key.StartsWith(prefix))
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(objects);
    }

    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_isHealthy);
    }

    // Test helpers
    public void SetHealthy(bool isHealthy) => _isHealthy = isHealthy;

    public int GetBucketCount() => _buckets.Count;

    public void Clear() => _buckets.Clear();
}
