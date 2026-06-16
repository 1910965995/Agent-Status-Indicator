namespace AgentStatusIndicator.Models;

public class AppConfig
{
    public string StatusFilePath { get; init; } = "";
    public double RingSize { get; init; } = 40;
    public double StrokeThickness { get; init; } = 4;
    public int TimeoutSeconds { get; init; } = 10;
    public int CompletedHoldSeconds { get; init; } = 10;
    public int ErrorHoldSeconds { get; init; } = 60;
    public StatusColors Colors { get; init; } = new();
}

public class StatusColors
{
    public string Idle { get; init; } = "#4caf50";
    public string Running { get; init; } = "#ffc107";
    public string Completed { get; init; } = "#2196f3";
    public string Error { get; init; } = "#f44336";
}
