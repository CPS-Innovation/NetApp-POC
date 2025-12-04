using Cps.S3Spike.Clients;
using Cps.S3Spike.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using Cps.S3Spike.Models;

namespace Cps.S3Spike.Functions;

public class UploadObject(INetAppClient netAppClient, IS3Client s3Client, IOptions<NetAppOptions> netAppOptions)
{
    private readonly INetAppClient _netAppClient = netAppClient;
    private readonly IS3Client _s3Client = s3Client;
    private readonly string _username = netAppOptions.Value.DefaultUsername;

    [Function(nameof(UploadObject))]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "netapp/upload")] HttpRequest req, FunctionContext functionContext)
    {
        // In a real implementation, you would extract the access token from the request's Authorization header
        string accessToken = req.Headers.Authorization.FirstOrDefault()?.Split(' ').Last() ?? string.Empty;

        try
        {
            if (string.IsNullOrWhiteSpace(_username))
            {
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            var response = await _netAppClient.RegenerateUserKeysAsync(_username, accessToken);

            if (response == null || response.Records.Count == 0)
            {
                return new NotFoundResult();
            }

            var form = await req.ReadFormAsync();
            var file = form.Files["file"];

            if (file == null || file.Length == 0)
            {
                return new BadRequestObjectResult("No file uploaded.");
            }

            var result = await _s3Client.PutObjectAsync("poc-bucket1", $"folder3/{file.FileName}", file.OpenReadStream(), response?.Records[0]?.AccessKey, response?.Records[0]?.SecretKey);

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
}
