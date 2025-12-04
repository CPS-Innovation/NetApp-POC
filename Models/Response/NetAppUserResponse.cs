using System.Text.Json.Serialization;
using Cps.S3Spike.Models.Dtos;

namespace Cps.S3Spike.Models.Response;

public class NetAppUserResponse
{
    [JsonPropertyName("num_records")]
    public int NumberOfRecords { get; set; }
    [JsonPropertyName("records")]
    public List<NetAppUserRecord> Records { get; set; } = [];
}