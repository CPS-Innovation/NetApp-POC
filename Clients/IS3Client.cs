using Amazon.S3.Model;

namespace Cps.S3Spike.Clients;

public interface IS3Client
{
    Task<List<string>> ListObjectsAsync(string bucketName, string accessKey, string secretKey);
    Task<List<string>> ListBuckets(string accessKey, string secretKey);
    Task<bool> PutObjectAsync(string bucketName, string objectKey, Stream objectData, string accessKey, string secretKey);
    Task<bool> CreateFolderAsync(string bucketName, string objectKey, string accessKey, string secretKey);
    Task<bool> DeleteObjectAsync(string bucketName, string objectKey, string accessKey, string secretKey);
    Task<GetObjectResponse> GetObjectAsync(string bucketName, string objectKey, string accessKey, string secretKey);
    Task<string> GeneratePresignedURLAsync(string bucketName, string objectKey, string accessKey, string secretKey);
    Task<InitiateMultipartUploadResponse?> InitiateMultipartUploadAsync(string bucketName, string objectKey, string accessKey, string secretKey);
    Task<UploadPartResponse?> UploadPartAsync(string bucketName, string objectKey, int partNumber, string uploadId, byte[] partData, string accessKey, string secretKey);
    Task<CompleteMultipartUploadResponse?> CompleteMultipartUploadAsync(string bucketName, string objectKey, string uploadId, List<PartETag> partETags, string accessKey, string secretKey);
}