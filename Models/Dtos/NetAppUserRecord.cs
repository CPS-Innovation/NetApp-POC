using System.Text.Json.Serialization;

namespace Cps.S3Spike.Models.Dtos;

public class NetAppUserRecord
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }
    [JsonPropertyName("access_key")]
    public string? AccessKey { get; set; }
    [JsonPropertyName("secret_key")]
    public string? SecretKey { get; set; }
}