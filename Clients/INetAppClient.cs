using Cps.S3Spike.Models.Response;

namespace Cps.S3Spike.Clients;

public interface INetAppClient
{
    Task<NetAppUserResponse> AddUserAsync(string username, string accessToken);
    Task<NetAppUserResponse> RegenerateUserKeysAsync(string username, string accessToken);
}
