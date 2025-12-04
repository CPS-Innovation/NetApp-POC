using System.Net;

namespace Cps.S3Spike.Exceptions;

public class NetAppClientException(HttpStatusCode statusCode, HttpRequestException httpRequestException) : Exception($"The HTTP request failed with status code {statusCode}", httpRequestException)
{
    public HttpStatusCode StatusCode { get; private set; } = statusCode;
}