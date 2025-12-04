namespace Cps.S3Spike.Clients;

public interface IS3HttpClient
{
    Task<HttpResponseMessage> PutObjectAsync(string bucketName, string objectKey, string accessKey, string secretKey);
}