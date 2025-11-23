using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace HotSwap.Distributed.Infrastructure.Storage;

/// <summary>
/// MinIO implementation of object storage service.
/// Provides S3-compatible object storage operations using MinIO SDK.
/// </summary>
public class MinioObjectStorageService : IObjectStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly MinioConfiguration _config;
    private readonly ILogger<MinioObjectStorageService> _logger;

    public MinioObjectStorageService(
        ILogger<MinioObjectStorageService> logger,
        IOptions<MinioConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;

        // Initialize MinIO client
        _minioClient = new MinioClient()
            .WithEndpoint(_config.Endpoint)
            .WithCredentials(_config.AccessKey, _config.SecretKey)
            .WithSSL(_config.UseSSL)
            .WithTimeout(_config.TimeoutSeconds * 1000) // Convert seconds to milliseconds
            .Build();

        _logger.LogInformation("MinIO object storage initialized. Endpoint: {Endpoint}, SSL: {UseSSL}",
            _config.Endpoint, _config.UseSSL);
    }

    public async Task<bool> CreateBucketAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if bucket already exists
            var exists = await BucketExistsAsync(bucketName, cancellationToken);
            if (exists)
            {
                _logger.LogDebug("Bucket {BucketName} already exists", bucketName);
                return false;
            }

            // Create the bucket
            var args = new MakeBucketArgs()
                .WithBucket(bucketName);

            if (!string.IsNullOrEmpty(_config.Region))
            {
                args = args.WithLocation(_config.Region);
            }

            await _minioClient.MakeBucketAsync(args, cancellationToken);

            _logger.LogInformation("Created bucket: {BucketName}", bucketName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create bucket: {BucketName}", bucketName);
            throw;
        }
    }

    public async Task DeleteBucketAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        try
        {
            // First, delete all objects in the bucket
            var objects = await ListObjectsAsync(bucketName, cancellationToken: cancellationToken);
            foreach (var objectName in objects)
            {
                await DeleteObjectAsync(bucketName, objectName, cancellationToken);
            }

            // Now delete the bucket
            var args = new RemoveBucketArgs()
                .WithBucket(bucketName);

            await _minioClient.RemoveBucketAsync(args, cancellationToken);

            _logger.LogInformation("Deleted bucket: {BucketName}", bucketName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete bucket: {BucketName}", bucketName);
            throw;
        }
    }

    public async Task<bool> BucketExistsAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        try
        {
            var args = new BucketExistsArgs()
                .WithBucket(bucketName);

            var exists = await _minioClient.BucketExistsAsync(args, cancellationToken);
            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if bucket exists: {BucketName}", bucketName);
            throw;
        }
    }

    public async Task<string> UploadObjectAsync(
        string bucketName,
        string objectName,
        Stream data,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var args = new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithStreamData(data)
                .WithObjectSize(data.Length)
                .WithContentType(contentType);

            await _minioClient.PutObjectAsync(args, cancellationToken);

            var objectUrl = _config.UseSSL
                ? $"https://{_config.Endpoint}/{bucketName}/{objectName}"
                : $"http://{_config.Endpoint}/{bucketName}/{objectName}";

            _logger.LogInformation("Uploaded object: {ObjectName} to bucket {BucketName}, Size: {Size} bytes",
                objectName, bucketName, data.Length);

            return objectUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload object: {ObjectName} to bucket {BucketName}",
                objectName, bucketName);
            throw;
        }
    }

    public async Task<Stream> DownloadObjectAsync(
        string bucketName,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var memoryStream = new MemoryStream();

            var args = new GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithCallbackStream(stream =>
                {
                    stream.CopyTo(memoryStream);
                });

            await _minioClient.GetObjectAsync(args, cancellationToken);

            memoryStream.Position = 0; // Reset stream position for reading

            _logger.LogDebug("Downloaded object: {ObjectName} from bucket {BucketName}, Size: {Size} bytes",
                objectName, bucketName, memoryStream.Length);

            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download object: {ObjectName} from bucket {BucketName}",
                objectName, bucketName);
            throw;
        }
    }

    public async Task DeleteObjectAsync(
        string bucketName,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var args = new RemoveObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName);

            await _minioClient.RemoveObjectAsync(args, cancellationToken);

            _logger.LogInformation("Deleted object: {ObjectName} from bucket {BucketName}",
                objectName, bucketName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete object: {ObjectName} from bucket {BucketName}",
                objectName, bucketName);
            throw;
        }
    }

    public async Task<long> GetBucketSizeAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        try
        {
            long totalSize = 0;

            var args = new ListObjectsArgs()
                .WithBucket(bucketName)
                .WithRecursive(true);

            var observable = _minioClient.ListObjectsEnumAsync(args, cancellationToken);

            await foreach (var item in observable.WithCancellation(cancellationToken))
            {
                if (item.IsDir)
                    continue;

                totalSize += (long)item.Size;
            }

            _logger.LogDebug("Bucket {BucketName} total size: {Size} bytes", bucketName, totalSize);
            return totalSize;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get bucket size: {BucketName}", bucketName);
            throw;
        }
    }

    public async Task<int> GetObjectCountAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        try
        {
            int count = 0;

            var args = new ListObjectsArgs()
                .WithBucket(bucketName)
                .WithRecursive(true);

            var observable = _minioClient.ListObjectsEnumAsync(args, cancellationToken);

            await foreach (var item in observable.WithCancellation(cancellationToken))
            {
                if (!item.IsDir)
                    count++;
            }

            _logger.LogDebug("Bucket {BucketName} object count: {Count}", bucketName, count);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get object count: {BucketName}", bucketName);
            throw;
        }
    }

    public async Task<IReadOnlyList<string>> ListObjectsAsync(
        string bucketName,
        string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var objectNames = new List<string>();

            var args = new ListObjectsArgs()
                .WithBucket(bucketName)
                .WithRecursive(true);

            if (!string.IsNullOrEmpty(prefix))
            {
                args = args.WithPrefix(prefix);
            }

            var observable = _minioClient.ListObjectsEnumAsync(args, cancellationToken);

            await foreach (var item in observable.WithCancellation(cancellationToken))
            {
                if (!item.IsDir)
                    objectNames.Add(item.Key);
            }

            _logger.LogDebug("Listed {Count} objects in bucket {BucketName}", objectNames.Count, bucketName);
            return objectNames;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list objects in bucket: {BucketName}", bucketName);
            throw;
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to list buckets as a simple health check
            await _minioClient.ListBucketsAsync(cancellationToken);

            _logger.LogDebug("MinIO health check passed");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MinIO health check failed");
            return false;
        }
    }
}
