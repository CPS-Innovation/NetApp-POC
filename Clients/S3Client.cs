using System.Net;
using System.Security.Cryptography;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Cps.S3Spike.Clients;

public class S3Client : IS3Client
{
    public async Task<List<string>> ListObjectsAsync(string bucketName, string accessKey, string secretKey)
    {
        var list = new List<string>();

        var s3Client = CreateS3Client(accessKey, secretKey);

        ListObjectsV2Request request = new()
        {
            BucketName = bucketName,
            Delimiter = "/",
        };

        try
        {
            var response = await s3Client.ListObjectsV2Async(request);

            foreach (var obj in response.S3Objects)
            {
                Console.WriteLine($"Object Key: {obj.Key}");
                list.Add(obj.Key);
            }
        }
        catch (AmazonS3Exception ex)
        {
            Console.WriteLine($"Error occurred: {ex.Message}");
        }

        return list;
    }

    public async Task<List<string>> ListBuckets(string accessKey, string secretKey)
    {
        var list = new List<string>();

        var s3Client = CreateS3Client(accessKey, secretKey);

        try
        {
            var response = await s3Client.ListBucketsAsync();

            foreach (var bucket in response.Buckets)
            {
                Console.WriteLine($"Bucket Name: {bucket.BucketName}");
                list.Add(bucket.BucketName);
            }
        }
        catch (AmazonS3Exception ex)
        {
            Console.WriteLine($"Error occurred: {ex.Message}");
        }
        
        return list;
    }

    public async Task<bool> PutObjectAsync(string bucketName, string objectKey, Stream objectData, string accessKey, string secretKey)
    {
        var result = false;

        var s3Client = CreateS3Client(accessKey, secretKey);

        try
        {
            var md5Hash = CalculateMD5(objectData);

            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                InputStream = objectData,
                // MD5Digest = md5Hash,
                UseChunkEncoding = false,
                DisableDefaultChecksumValidation = true
            };

            var response = await s3Client.PutObjectAsync(request);

            result = response.HttpStatusCode == HttpStatusCode.OK;
        }
        catch (AmazonS3Exception ex)
        {
            Console.WriteLine($"Error occurred: {ex.Message}");
        }

        return result;
    }

    public async Task<bool> CreateFolderAsync(string bucketName, string folderName, string accessKey, string secretKey)
    {
        var result = false;

        var s3Client = CreateS3Client(accessKey, secretKey);

        try
        {
            var ms = new MemoryStream([]);
            var md5Hash = CalculateMD5(ms);
            
            var response = await s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = folderName,
                // InputStream = new MemoryStream([]),
                // ContentBody = string.Empty,
                // MD5Digest = md5Hash
            });

            result = response.HttpStatusCode == HttpStatusCode.OK;
        }
        catch (AmazonS3Exception ex)
        {
            Console.WriteLine($"Error occurred: {ex.Message}");
        }

        return result;    }

    public async Task<bool> DeleteObjectAsync(string bucketName, string objectKey, string accessKey, string secretKey)
    {
        var result = false;

        var s3Client = CreateS3Client(accessKey, secretKey);

        try
        {
            var response = await s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = bucketName,
                Key = objectKey
            });

            result = response.HttpStatusCode == HttpStatusCode.OK;
        }
        catch (AmazonS3Exception ex)
        {
            Console.WriteLine($"Error occurred: {ex.Message}");
        }

        return result;
    }

    public async Task<GetObjectResponse> GetObjectAsync(string bucketName, string objectKey, string accessKey, string secretKey)
    {
        var s3Client = CreateS3Client(accessKey, secretKey);

        try
        {
            return await s3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = bucketName,
                Key = objectKey,
            });

        }
        catch (AmazonS3Exception ex)
        {
            Console.WriteLine($"Error occurred: {ex.Message}");
            return new GetObjectResponse();
        }
    }

    public async Task<string> GeneratePresignedURLAsync(string bucketName, string objectKey, string accessKey, string secretKey)
    {
        var s3Client = CreateS3Client(accessKey, secretKey);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            Expires = DateTime.UtcNow.AddMinutes(15)
        };

        var url = await s3Client.GetPreSignedURLAsync(request);

        return url;
    }

    public Task<InitiateMultipartUploadResponse?> InitiateMultipartUploadAsync(string bucketName, string objectKey, string accessKey, string secretKey)
    {
        var s3Client = CreateS3Client(accessKey, secretKey);

        var request = new InitiateMultipartUploadRequest
        {
            BucketName = bucketName,
            Key = objectKey
        };

        return s3Client.InitiateMultipartUploadAsync(request);
    }

    public Task<UploadPartResponse?> UploadPartAsync(string bucketName, string objectKey, int partNumber, string uploadId, byte[] partData, string accessKey, string secretKey)
    {
        var s3Client = CreateS3Client(accessKey, secretKey);

        var request = new UploadPartRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            PartNumber = partNumber,
            UploadId = uploadId,
            PartSize = partData.Length,
            InputStream = new MemoryStream(partData)
        };

        return s3Client.UploadPartAsync(request);
    }

    public Task<CompleteMultipartUploadResponse?> CompleteMultipartUploadAsync(string bucketName, string objectKey, string uploadId, List<PartETag> partETags, string accessKey, string secretKey)
    {
        var s3Client = CreateS3Client(accessKey, secretKey);

        var request = new CompleteMultipartUploadRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            UploadId = uploadId,
            PartETags = partETags
        };
        
        return s3Client.CompleteMultipartUploadAsync(request);
    }

    private static AmazonS3Client CreateS3Client(string accessKey, string secretKey)
    {
        var credentials = new BasicAWSCredentials(accessKey, secretKey);

        ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
        };

        var customHttpClientFactory = new CustomHttpClientFactory(handler);

        var s3Client = new AmazonS3Client(credentials, new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.EUWest1,
            ServiceURL = "https://10.4.16.19/",
            ForcePathStyle = true,
            LogMetrics = true,
            LogResponse = true,
            HttpClientFactory = customHttpClientFactory,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            UseHttp = true
        });

        return s3Client;
    }

    private static string CalculateMD5(Stream memoryStream)
    {
        using var md5 = MD5.Create();
        // Read the file into a MemoryStream

        // Compute the MD5 hash from the MemoryStream
        byte[] hash = md5.ComputeHash(memoryStream);

        // Return the hash as a Base64 encoded string
        return Convert.ToBase64String(hash);
    }
}

public class CustomHttpClientFactory(HttpClientHandler handler) : HttpClientFactory
{
    private readonly HttpClientHandler _handler = handler;

    public override HttpClient CreateHttpClient(IClientConfig clientConfig)
    {
        return new HttpClient(_handler);
    }
}