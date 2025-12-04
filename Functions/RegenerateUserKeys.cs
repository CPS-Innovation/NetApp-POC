using System.Text.Json;
using Cps.S3Spike.Clients;
using Cps.S3Spike.Exceptions;
using Cps.S3Spike.Models.Request;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace Cps.S3Spike.Functions;

public class RegenerateUserKeys(INetAppClient netAppClient, IS3Client s3Client)
{
    private readonly INetAppClient _netAppClient = netAppClient;
    private readonly IS3Client _s3Client = s3Client;

    [Function(nameof(RegenerateUserKeys))]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "netapp/users")] HttpRequestData req, FunctionContext functionContext)
    {
        // In a real implementation, you would extract the access token from the request's Authorization header
        string accessToken = req.Headers.GetValues("Authorization").FirstOrDefault()?.Split(' ').Last() ?? string.Empty;

        using var reader = new StreamReader(req.Body);
        var requestJson = await reader.ReadToEndAsync();

        var request = JsonSerializer.Deserialize<NetAppUserRequest>(requestJson, GetJsonSerializerOptions());

        if (request == null)
        {
            return new BadRequestResult();
        }

        try
        {
            var response = await _netAppClient.RegenerateUserKeysAsync(request.Username, accessToken);

            if (response == null || response.Records.Count == 0)
            {
                return new NotFoundResult();
            }

            //var result = await _s3Client.ListBuckets(response?.Records[0]?.AccessKey, response?.Records[0]?.SecretKey);
            var result = await _s3Client.ListObjectsAsync("poc-bucket1", response?.Records[0]?.AccessKey, response?.Records[0]?.SecretKey);

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