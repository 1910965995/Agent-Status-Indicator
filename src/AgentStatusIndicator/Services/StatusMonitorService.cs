using System.IO;
using System.Text.Json;
using AgentStatusIndicator.Models;

namespace AgentStatusIndicator.Services;

public class StatusMonitorService : IDisposable
{
    private readonly string _filePath;
    private readonly string _watchDir;
    private FileSystemWatcher? _watcher;
    private Timer? _timeoutTimer;
    private bool _disposed;

    public event EventHandler<StatusChangedEventArgs>? StatusChanged;

    public string FilePath => _filePath;

    public StatusMonitorService(string filePath)
    {
        _filePath = filePath;
        _watchDir = Path.GetDirectoryName(filePath) ?? "";
    }

    public void Start()
    {
        // Read initial state
        var initial = ReadCurrentStatus();
        if (initial != null)
        {
            NotifyStatusChanged(ParseAgentStatus(initial.Status), initial.Task, ParseDateTime(initial.StartedAt));
        }

        // Start watching
        if (Directory.Exists(_watchDir))
        {
            _watcher = new FileSystemWatcher(_watchDir)
            {
                Filter = Path.GetFileName(_filePath),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime
            };
            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.EnableRaisingEvents = true;
        }

        // Start timeout timer (ticks every 5 seconds)
        _timeoutTimer = new Timer(OnTimeoutTick, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public StatusFileData? ReadCurrentStatus()
    {
        try
        {
            if (!File.Exists(_filePath))
                return null;

            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonSerializer.Deserialize<StatusFileData>(json);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce: FileSystemWatcher fires multiple events for one write
        Thread.Sleep(100);
        var data = ReadCurrentStatus();
        if (data == null) return;

        NotifyStatusChanged(ParseAgentStatus(data.Status), data.Task, ParseDateTime(data.StartedAt));
    }

    private void OnTimeoutTick(object? state)
    {
        // If no file update in TimeoutSeconds, switch to idle
        var data = ReadCurrentStatus();
        if (data == null)
        {
            NotifyStatusChanged(AgentStatus.Idle, null, null);
        }
    }

    private void NotifyStatusChanged(AgentStatus status, string? task, DateTime? startedAt)
    {
        StatusChanged?.Invoke(this, new StatusChangedEventArgs(status, task, startedAt));
    }

    private static AgentStatus ParseAgentStatus(string status) =>
        status.ToLowerInvariant() switch
        {
            "running" => AgentStatus.Running,
            "completed" => AgentStatus.Completed,
            "error" => AgentStatus.Error,
            _ => AgentStatus.Idle
        };

    private static DateTime? ParseDateTime(string? s) =>
        DateTime.TryParse(s, out var dt) ? dt : null;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watcher?.Dispose();
        _timeoutTimer?.Dispose();
    }
}
