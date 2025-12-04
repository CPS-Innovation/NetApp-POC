using Amazon.Runtime.Internal.Util;
using Amazon.S3.Model;
using Cps.S3Spike.Clients;
using Cps.S3Spike.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Cps.S3Spike.Functions;

public class UploadMultipartObject(ILogger<UploadMultipartObject> logger, INetAppClient netAppClient, IS3Client s3Client)
{
    private readonly ILogger<UploadMultipartObject> _logger = logger;
    private readonly INetAppClient _netAppClient = netAppClient;
    private readonly IS3Client _s3Client = s3Client;
    private const string BucketName = "flexgroup2";
    private const string FolderName = ""; // include trailing slash if needed

    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    [Function(nameof(UploadMultipartObject))]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "netapp/upload-multipart")] HttpRequest req, FunctionContext functionContext)
    {
        // In a real implementation, you would extract the access token from the request's Authorization header
        string accessToken = req.Headers.Authorization.FirstOrDefault()?.Split(' ').Last() ?? string.Empty;

        try
        {
            var response = await _netAppClient.RegenerateUserKeysAsync("neil.foubister@cps.gov.uk", accessToken);
            _logger.LogInformation("User keys regenerated successfully.");

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

            using var sourceStream = file.OpenReadStream();
            var session = await _s3Client.InitiateMultipartUploadAsync(BucketName, $"{FolderName}{file.FileName}", response?.Records[0]?.AccessKey, response?.Records[0]?.SecretKey);
            _logger.LogInformation("Multipart upload session initiated.");

            long totalSize = sourceStream.Length;
            long position = 0;
            int chunkSize = 1000000; // 1 MB
            int chunkNumber = 1;
            Dictionary<int, string> uploadedChunks = [];

            using var md5 = System.Security.Cryptography.MD5.Create();
            byte[] buffer = new byte[chunkSize];

            while (position < totalSize)
            {
                int bytesToRead = (int)Math.Min(chunkSize, totalSize - position);
                int bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, bytesToRead), CancellationToken.None);
                if (bytesRead <= 0)
                    break;

                long start = position;
                long end = start + bytesRead - 1;
                // string contentRange = $"{start}-{end}/{totalSize}";

                md5.TransformBlock(buffer, 0, bytesRead, null, 0);

                try
                {
                    _logger.LogInformation("Uploading part {ChunkNumber}, bytes {Start}-{End} of {TotalSize}.", chunkNumber, start, end, totalSize);
                    var result = await _s3Client.UploadPartAsync(BucketName, $"{FolderName}{file.FileName}", chunkNumber, session.UploadId, buffer[..bytesRead], response?.Records[0]?.AccessKey, response?.Records[0]?.SecretKey);
                    _logger.LogInformation("Uploaded part {ChunkNumber} with ETag {ETag} was successful.", chunkNumber, result.ETag);

                if (result.ETag != null)
                {
                    uploadedChunks.Add(result.PartNumber.Value, result.ETag);
                }

                position += bytesRead;
                chunkNumber++;

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                    throw;
                }
            }

            var completedParts = uploadedChunks.Select(part => new PartETag
            {
                PartNumber = part.Key,
                ETag = part.Value
            }).ToList();

            _logger.LogInformation("Completing multipart upload for file {FileName}.", file.FileName);
            var completedResult = await _s3Client.CompleteMultipartUploadAsync(BucketName, $"{FolderName}{file.FileName}", session.UploadId, completedParts, response?.Records[0]?.AccessKey, response?.Records[0]?.SecretKey);
            _logger.LogInformation("Multipart upload completed successfully for file {FileName}.", file.FileName);

            return new OkObjectResult(completedResult);
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