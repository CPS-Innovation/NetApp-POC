namespace Cps.S3Spike.Factories;

public interface INetAppRequestFactory
{
    HttpRequestMessage CreateAddUserRequest(string username, string accessToken);
    HttpRequestMessage CreateRegenerateUserKeysRequest(string username, string accessToken);
}