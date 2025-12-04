using System.Text.Json.Serialization;

namespace Cps.S3Spike.Models.Dtos;

public class RegenerateKeysDto
{
    [JsonPropertyName("regenerate_keys")]
    public required string RegenerateKeys { get; set; }
}