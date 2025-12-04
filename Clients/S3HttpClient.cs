using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cps.S3Spike.Exceptions;

namespace Cps.S3Spike.Clients;

public class S3HttpClient(HttpClient httpClient) : IS3HttpClient
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<HttpResponseMessage> PutObjectAsync(string bucketName, string objectKey, string accessKey, string secretKey)
    {
        var url = $"{bucketName}/{objectKey}";
        var signature = getSignatureKey(secretKey, url);
        var request = BuildRequest<object>(HttpMethod.Put, $"{bucketName}/{objectKey}", accessKey, signature);// Implementation for putting an object into S3 using HTTP requests
        return await CallNetApp(request);
    }
 
    private async Task<T> CallNetApp<T>(HttpRequestMessage request)
    {
        using var response = await CallNetApp(request);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<T>(content) ?? throw new InvalidOperationException("Deserialization returned null.");
        return result;
    }

    private async Task<HttpResponseMessage> CallNetApp(HttpRequestMessage request, params HttpStatusCode[] expectedUnhappyStatusCodes)
    {
        var response = await _httpClient.SendAsync(request);
        try
        {
            if (response.IsSuccessStatusCode || expectedUnhappyStatusCodes.Contains(response.StatusCode))
            {
                return response;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new NetAppUnauthorizedException();
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                throw new NetAppConflictException();
            }

            var content = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(content);
        }
        catch (HttpRequestException exception)
        {
            throw new NetAppClientException(response.StatusCode, exception);
        }
    }

    private static HttpRequestMessage BuildRequest<T>(HttpMethod method, string path, string accessKey, string signature, T? body = default)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Authorization", $"AWS {accessKey}:{signature}");
        request.Headers.Add("Accept", "application/json");
        if (body != null)
        {
            request.Content =new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }
        return request;
    }

    static byte[] HmacSHA256(String data, byte[] key)
    {
        HMACSHA256 kha = new(key)
        {
            Key = key
        };

        return kha.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string getSignatureKey(string key, string stringToSign)
    {
        byte[] kSecret = Encoding.UTF8.GetBytes(key.ToCharArray());
        byte[] kSigning = HmacSHA256(stringToSign, kSecret);
        return WebUtility.UrlEncode(Convert.ToBase64String(kSigning));
    }

}