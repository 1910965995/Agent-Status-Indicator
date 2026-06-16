using AgentStatusIndicator.Models;

namespace AgentStatusIndicator.Services;

public class StatusChangedEventArgs : EventArgs
{
    public AgentStatus Status { get; }
    public string? Task { get; }
    public DateTime? StartedAt { get; }

    public StatusChangedEventArgs(AgentStatus status, string? task, DateTime? startedAt)
    {
        Status = status;
        Task = task;
        StartedAt = startedAt;
    }
}
