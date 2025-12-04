
using System.Text;
using System.Text.Json;
using System.Web;
using Cps.S3Spike.Models;
using Cps.S3Spike.Models.Dtos;

namespace Cps.S3Spike.Factories;

public class NetAppRequestFactory : INetAppRequestFactory
{
    public HttpRequestMessage CreateAddUserRequest(string username, string accessToken)
    {
        return BuildRequest<NetAppOptions>(HttpMethod.Post, $"api/protocols/s3/services/895799e5-6c97-11f0-972a-002248c7e77a/users/{EncodedValue(username)}", accessToken);
    }

    public HttpRequestMessage CreateRegenerateUserKeysRequest(string username, string accessToken)
    {
        var regenerateKeys = new RegenerateKeysDto
        {
            RegenerateKeys = "True"
        };
        return BuildRequest(HttpMethod.Patch, $"api/protocols/s3/services/895799e5-6c97-11f0-972a-002248c7e77a/users/{EncodedValue(username)}", accessToken, regenerateKeys);
    }

    private static HttpRequestMessage BuildRequest<T>(HttpMethod method, string path, string accessToken, T? body = default)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Headers.Add("Accept", "application/json");
        if (body != null)
        {
            request.Content =new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }
        return request;
    }

    private static string EncodedValue(string value)
    {
        return HttpUtility.UrlEncode(value);
    }
}