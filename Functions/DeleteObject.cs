using System.Text.Json;
using Cps.S3Spike.Clients;
using Cps.S3Spike.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Cps.S3Spike.Functions;

public class DeleteObject(INetAppClient netAppClient, IS3Client s3Client)
{
    private readonly INetAppClient _netAppClient = netAppClient;
    private readonly IS3Client _s3Client = s3Client;

    [Function(nameof(DeleteObject))]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "netapp")] HttpRequest req, FunctionContext functionContext)
    {
        // In a real implementation, you would extract the access token from the request's Authorization header
        string accessToken = req.Headers.Authorization.FirstOrDefault()?.Split(' ').Last() ?? string.Empty;

        try
        {
            var response = await _netAppClient.RegenerateUserKeysAsync("neil.foubister@cps.gov.uk", accessToken);

            if (response == null || response.Records.Count == 0)
            {
                return new NotFoundResult();
            }

            var result = await _s3Client.DeleteObjectAsync("poc-bucket1", $"folder3/host.json", response?.Records[0]?.AccessKey, response?.Records[0]?.SecretKey);

            return new OkObjectResult(result);
        }
        catch (NetAppUnauthorizedException)
        {
            return new UnauthorizedResult();
        }
        catch (NetAppConflictException)
        {
            return new ConflictResult();
        }
        catch (NetAppClientException)
        {
            throw;
        }
    }
    
    private static JsonSerializerOptions GetJsonSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
    }
}