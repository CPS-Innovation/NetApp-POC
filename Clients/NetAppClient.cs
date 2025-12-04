using System.Net;
using System.Text.Json;
using Cps.S3Spike.Exceptions;
using Cps.S3Spike.Factories;
using Cps.S3Spike.Models.Response;

namespace Cps.S3Spike.Clients;

public class NetAppClient(HttpClient httpClient, INetAppRequestFactory netAppRequestFactory) : INetAppClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly INetAppRequestFactory _netAppRequestFactory = netAppRequestFactory;

    public Task<NetAppUserResponse> AddUserAsync(string username, string accessToken)
    {
        var request = _netAppRequestFactory.CreateAddUserRequest(username, accessToken);
        return CallNetApp<NetAppUserResponse>(request);
    }

    public Task<NetAppUserResponse> RegenerateUserKeysAsync(string username, string accessToken)
    {
        var request = _netAppRequestFactory.CreateRegenerateUserKeysRequest(username, accessToken);
        return CallNetApp<NetAppUserResponse>(request);
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
}