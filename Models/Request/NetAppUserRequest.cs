using System.Text.Json.Serialization;

namespace Cps.S3Spike.Models.Request;

public class NetAppUserRequest
{
    [JsonPropertyName("name")]
    public required string Username { get; set; }
}