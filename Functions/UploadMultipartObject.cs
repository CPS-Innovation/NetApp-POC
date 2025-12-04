using Amazon.S3.Model;
using Cps.S3Spike.Clients;
using Cps.S3Spike.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Cps.S3Spike.Models;

namespace Cps.S3Spike.Functions;

public class UploadMultipartObject(
    ILogger<UploadMultipartObject> logger,
    INetAppClient netAppClient,
    IS3Client s3Client,
    IOptions<NetAppOptions> netAppOptions)
{
    private readonly ILogger<UploadMultipartObject> _logger = logger;
    private readonly INetAppClient _netAppClient = netAppClient;
    private readonly IS3Client _s3Client = s3Client;
    private readonly string _username = netAppOptions.Value.DefaultUsername;
    private const string BucketName = "flexgroup2";
    private const string FolderName = ""; // include trailing slash if needed

    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    [Function(nameof(UploadMultipartObject))]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "netapp/upload-multipart")]
        HttpRequest req, FunctionContext functionContext)
    {
        // In a real implementation, you would extract the access token from the request's Authorization header
        string accessToken = req.Headers.Authorization.FirstOrDefault()?.Split(' ').Last() ?? string.Empty;

        try
        {
            if (string.IsNullOrWhiteSpace(_username))
            {
                _logger.LogError("Default username is not configured.");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            // Get filename from query parameter to avoid form parsing
            string? fileName = req.Query["filename"];
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return new BadRequestObjectResult("Filename query parameter is required.");
            }

            // Get content length
            if (!req.ContentLength.HasValue || req.ContentLength.Value == 0)
            {
                return new BadRequestObjectResult("Request body is empty.");
            }

            var response = await _netAppClient.RegenerateUserKeysAsync(_username, accessToken);
            _logger.LogInformation("User keys regenerated successfully.");

            if (response == null || response.Records.Count == 0)
            {
                return new NotFoundResult();
            }

            long totalSize = req.ContentLength.Value;
            const int oneMb = 1024 * 1024;
            const int minMultipartSize = 5 * oneMb; // S3 requires 5 MB minimum per part (except the last)
            const int targetPartSize = 8 * oneMb; // Aim for 8 MB aligned parts

            // If the file is small enough, avoid multipart entirely to prevent S3 validation errors
            if (totalSize <= minMultipartSize)
            {
                _logger.LogInformation("File size {TotalSize} <= {MinMultipartSize} bytes, using single PUT.",
                    totalSize, minMultipartSize);
                if (req.Body.CanSeek)
                {
                    req.Body.Position = 0;
                }

                var singleResult = await _s3Client.PutObjectAsync(BucketName, $"{FolderName}{fileName}", req.Body,
                    response?.Records[0]?.AccessKey, response?.Records[0]?.SecretKey);
                return new OkObjectResult(singleResult);
            }

            // Read directly from request body stream (no form parsing to avoid proxy buffering issues)
            var session = await _s3Client.InitiateMultipartUploadAsync(BucketName, $"{FolderName}{fileName}",
                response?.Records[0]?.AccessKey, response?.Records[0]?.SecretKey);
            _logger.LogInformation("Multipart upload session initiated for file {FileName}.", fileName);

            if (req.Body.CanSeek)
            {
                req.Body.Position = 0; // ensure buffered body starts from beginning
            }

            long bytesUploaded = 0;
            long bytesProcessed = 0;
            int chunkNumber = 1;
            Dictionary<int, string> uploadedChunks = [];

            // Read in 1 MB blocks to keep parts 1 MB aligned
            byte[] buffer = new byte[oneMb];
            using var partBuffer = new MemoryStream(targetPartSize);

            while (bytesProcessed < totalSize)
            {
                int bytesToRead = (int)Math.Min(buffer.Length, totalSize - bytesProcessed);
                int bytesRead = await req.Body.ReadAsync(buffer.AsMemory(0, bytesToRead), CancellationToken.None);
                if (bytesRead <= 0)
                {
                    break;
                }

                partBuffer.Write(buffer, 0, bytesRead);
                bytesProcessed += bytesRead;

                long remaining = totalSize - bytesProcessed;
                bool isLastPart = remaining == 0;

                // Flush when:
                // - we reached or exceeded the target size and are aligned on 1 MB, or
                // - this is the last part and there's data to send, or
                // - we have at least the minimum part size and the remaining data would be smaller than the minimum (so send now)
                bool alignedToOneMb = partBuffer.Length % oneMb == 0;
                bool readyAtTarget = partBuffer.Length >= targetPartSize && alignedToOneMb;
                bool readyAtEnd = isLastPart && partBuffer.Length > 0;
                bool preventTinyRemainder = partBuffer.Length >= minMultipartSize && remaining > 0 &&
                                            remaining < minMultipartSize && alignedToOneMb;

                if (readyAtTarget || readyAtEnd || preventTinyRemainder)
                {
                    var partBytes = partBuffer.ToArray();
                    long start = bytesUploaded;
                    long end = start + partBytes.Length - 1;

                    try
                    {
                        _logger.LogInformation("Uploading part {ChunkNumber}, bytes {Start}-{End} of {TotalSize}.",
                            chunkNumber, start, end, totalSize);
                        var result = await _s3Client.UploadPartAsync(BucketName, $"{FolderName}{fileName}", chunkNumber,
                            session.UploadId, partBytes, response?.Records[0]?.AccessKey,
                            response?.Records[0]?.SecretKey);
                        _logger.LogInformation("Uploaded part {ChunkNumber} with ETag {ETag} was successful.",
                            chunkNumber, result.ETag);

                        if (result.ETag != null)
                        {
                            uploadedChunks.Add(result.PartNumber.Value, result.ETag);
                        }

                        bytesUploaded += partBytes.Length;
                        chunkNumber++;
                        partBuffer.SetLength(0);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.Message);
                        throw;
                    }
                }
            }

            if (uploadedChunks.Count == 0)
            {
                _logger.LogError("Multipart upload aborted: no parts were uploaded for file {FileName}.", fileName);
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            var completedParts = uploadedChunks
                .OrderBy(part => part.Key)
                .Select(part => new PartETag
                {
                    PartNumber = part.Key,
                    ETag = part.Value
                }).ToList();

            _logger.LogInformation("Completing multipart upload for file {FileName}.", fileName);
            var completedResult = await _s3Client.CompleteMultipartUploadAsync(BucketName, $"{FolderName}{fileName}",
                session.UploadId, completedParts, response?.Records[0]?.AccessKey, response?.Records[0]?.SecretKey);
            _logger.LogInformation("Multipart upload completed successfully for file {FileName}.", fileName);

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