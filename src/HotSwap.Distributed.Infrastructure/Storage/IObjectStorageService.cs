namespace HotSwap.Distributed.Infrastructure.Storage;

/// <summary>
/// Abstraction for object storage operations.
/// Provides a unified interface for S3-compatible storage (MinIO, AWS S3, etc.)
/// </summary>
public interface IObjectStorageService
{
    /// <summary>
    /// Creates a new storage bucket for a tenant.
    /// </summary>
    /// <param name="bucketName">Name of the bucket (must be DNS-compatible)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if bucket was created, false if it already existed</returns>
    Task<bool> CreateBucketAsync(string bucketName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a storage bucket and all its contents.
    /// </summary>
    /// <param name="bucketName">Name of the bucket to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteBucketAsync(string bucketName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a bucket exists.
    /// </summary>
    /// <param name="bucketName">Name of the bucket</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if bucket exists</returns>
    Task<bool> BucketExistsAsync(string bucketName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads an object to storage.
    /// </summary>
    /// <param name="bucketName">Target bucket name</param>
    /// <param name="objectName">Object key/path in the bucket</param>
    /// <param name="data">Data stream to upload</param>
    /// <param name="contentType">MIME type of the content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>URL or identifier of the uploaded object</returns>
    Task<string> UploadObjectAsync(
        string bucketName,
        string objectName,
        Stream data,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads an object from storage.
    /// </summary>
    /// <param name="bucketName">Source bucket name</param>
    /// <param name="objectName">Object key/path in the bucket</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream containing the object data</returns>
    Task<Stream> DownloadObjectAsync(
        string bucketName,
        string objectName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an object from storage.
    /// </summary>
    /// <param name="bucketName">Source bucket name</param>
    /// <param name="objectName">Object key/path to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteObjectAsync(
        string bucketName,
        string objectName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total size of all objects in a bucket (in bytes).
    /// </summary>
    /// <param name="bucketName">Bucket name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total size in bytes</returns>
    Task<long> GetBucketSizeAsync(string bucketName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of objects in a bucket.
    /// </summary>
    /// <param name="bucketName">Bucket name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of objects</returns>
    Task<int> GetObjectCountAsync(string bucketName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all objects in a bucket.
    /// </summary>
    /// <param name="bucketName">Bucket name</param>
    /// <param name="prefix">Optional prefix to filter objects</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of object names</returns>
    Task<IReadOnlyList<string>> ListObjectsAsync(
        string bucketName,
        string? prefix = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks connectivity to the storage service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if connection is healthy</returns>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}
