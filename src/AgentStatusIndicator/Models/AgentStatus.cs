using System.Text.Json.Serialization;

namespace AgentStatusIndicator.Models;

public enum AgentStatus
{
    Idle,
    Running,
    Completed,
    Error
}

public record StatusFileData
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "idle";
    [JsonPropertyName("task")]
    public string? Task { get; init; }
    [JsonPropertyName("started_at")]
    public string? StartedAt { get; init; }
}
