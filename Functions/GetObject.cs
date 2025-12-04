using Amazon.S3;
using Cps.S3Spike.Clients;
using Cps.S3Spike.Exceptions;
using Cps.S3Spike.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace Cps.S3Spike.Functions;

public class GetObject(INetAppClient netAppClient, IS3Client s3Client, IOptions<NetAppOptions> netAppOptions)
{
    private readonly INetAppClient _netAppClient = netAppClient;
    private readonly IS3Client _s3Client = s3Client;
    private readonly string _username = netAppOptions.Value.DefaultUsername;

    [Function(nameof(GetObject))]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "netapp/{filename}")] HttpRequest req, FunctionContext functionContext, string filename)
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

            var result = await _s3Client.GetObjectAsync("poc-bucket1", $"folder3/{filename}", response?.Records[0]?.AccessKey, response?.Records[0]?.SecretKey);

            var contentType = result.Headers["Content-Type"];

            return new FileStreamResult(result.ResponseStream, contentType);
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "HashCheckFailed")
        {
            // Handle hash mismatch gracefully
            Console.WriteLine("Hash mismatch, but file downloaded successfully.");
            // Optionally proceed with the file
            return new OkObjectResult("File downloaded with hash mismatch.");
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
