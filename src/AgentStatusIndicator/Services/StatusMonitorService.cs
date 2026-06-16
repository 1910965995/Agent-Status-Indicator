using System.IO;
using System.Text.Json;
using AgentStatusIndicator.Models;

namespace AgentStatusIndicator.Services;

public class StatusMonitorService : IDisposable
{
    private readonly string _filePath;
    private readonly string _watchDir;
    private FileSystemWatcher? _hookWatcher;
    private Timer? _pollTimer;
    private bool _disposed;

    // Session file tracking
    private string? _sessionFilePath;
    private DateTime _lastSessionWrite = DateTime.MinValue;
    private DateTime _lastHookWrite = DateTime.MinValue;

    // Current known statuses
    private AgentStatus _hookStatus = AgentStatus.Idle;
    private string? _hookTask;
    private DateTime? _hookStartedAt;
    private AgentStatus _lastNotifiedStatus = AgentStatus.Idle;

    private static readonly TimeSpan SessionRunningWindow = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan SessionIdleTimeout = TimeSpan.FromSeconds(5);

    public event EventHandler<StatusChangedEventArgs>? StatusChanged;

    public string FilePath => _filePath;

    public StatusMonitorService(string filePath)
    {
        _filePath = filePath;
        _watchDir = Path.GetDirectoryName(filePath) ?? "";
    }

    public void Start()
    {
        // Find the session file to monitor
        _sessionFilePath = FindNewestSessionFile();
        if (_sessionFilePath != null)
            _lastSessionWrite = File.GetLastWriteTime(_sessionFilePath);

        // Read initial hook state
        var initial = ReadCurrentStatus();
        if (initial != null)
        {
            _hookStatus = ParseAgentStatus(initial.Status);
            _hookTask = initial.Task;
            _hookStartedAt = ParseDateTime(initial.StartedAt);
            _lastHookWrite = File.GetLastWriteTime(_filePath);
            Notify(_hookStatus, _hookTask, _hookStartedAt);
        }

        // Watch the hook status file (event-driven, fast)
        if (Directory.Exists(_watchDir))
        {
            _hookWatcher = new FileSystemWatcher(_watchDir)
            {
                Filter = Path.GetFileName(_filePath),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime
            };
            _hookWatcher.Changed += OnHookFileChanged;
            _hookWatcher.Created += OnHookFileChanged;
            _hookWatcher.EnableRaisingEvents = true;
        }

        // Polling timer: checks session file activity every second
        _pollTimer = new Timer(OnPollTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public StatusFileData? ReadCurrentStatus()
    {
        try
        {
            if (!File.Exists(_filePath)) return null;
            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonSerializer.Deserialize<StatusFileData>(json);
        }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
    }

    private void OnHookFileChanged(object sender, FileSystemEventArgs e)
    {
        Thread.Sleep(100);
        var data = ReadCurrentStatus();
        if (data == null) return;

        _hookStatus = ParseAgentStatus(data.Status);
        _hookTask = data.Task;
        _hookStartedAt = ParseDateTime(data.StartedAt);
        _lastHookWrite = File.GetLastWriteTime(_filePath);

        // Hook says completed/error → always respect it
        if (_hookStatus == AgentStatus.Completed || _hookStatus == AgentStatus.Error)
        {
            Notify(_hookStatus, _hookTask, _hookStartedAt);
        }
        // Hook says running → show running
        else if (_hookStatus == AgentStatus.Running)
        {
            Notify(AgentStatus.Running, _hookTask, _hookStartedAt);
        }
        // Hook says idle → let poll timer decide
    }

    private void OnPollTick(object? state)
    {
        // Re-find session file if lost (handles new sessions)
        if (_sessionFilePath == null || !File.Exists(_sessionFilePath))
        {
            _sessionFilePath = FindNewestSessionFile();
        }

        // Check session file modification time
        if (_sessionFilePath != null && File.Exists(_sessionFilePath))
        {
            try
            {
                var sessionWrite = File.GetLastWriteTime(_sessionFilePath);
                if (sessionWrite > _lastSessionWrite)
                {
                    _lastSessionWrite = sessionWrite;
                }
            }
            catch { /* file might be locked */ }
        }

        var sessionActive = (DateTime.Now - _lastSessionWrite) < SessionIdleTimeout;
        var sessionRecentlyActive = (DateTime.Now - _lastSessionWrite) < SessionRunningWindow;

        // Priority: hook completed/error > session active > hook idle
        if (_hookStatus == AgentStatus.Completed || _hookStatus == AgentStatus.Error)
        {
            // Show completed/error, but check if it's stale (> 10s)
            // MainWindow handles the hold time via auto-hide timer
            if (_lastNotifiedStatus != _hookStatus)
                Notify(_hookStatus, _hookTask, _hookStartedAt);
            return;
        }

        // Session file recently modified → Claude is working
        if (sessionRecentlyActive)
        {
            if (_lastNotifiedStatus != AgentStatus.Running)
                Notify(AgentStatus.Running, "Claude 工作中...", null);
            return;
        }

        // Session file not recently modified + hook says running → keep running
        if (_hookStatus == AgentStatus.Running && sessionActive)
        {
            if (_lastNotifiedStatus != AgentStatus.Running)
                Notify(AgentStatus.Running, _hookTask, _hookStartedAt);
            return;
        }

        // Nothing active → idle
        if (_lastNotifiedStatus != AgentStatus.Idle)
            Notify(AgentStatus.Idle, null, null);
    }

    private void Notify(AgentStatus status, string? task, DateTime? startedAt)
    {
        _lastNotifiedStatus = status;
        StatusChanged?.Invoke(this, new StatusChangedEventArgs(status, task, startedAt));
    }

    private static string? FindNewestSessionFile()
    {
        try
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var projectsDir = Path.Combine(homeDir, ".claude", "projects");
            if (!Directory.Exists(projectsDir)) return null;

            string? newest = null;
            var newestTime = DateTime.MinValue;

            foreach (var dir in Directory.GetDirectories(projectsDir))
            {
                foreach (var f in Directory.GetFiles(dir, "*.jsonl"))
                {
                    var t = File.GetLastWriteTime(f);
                    if (t > newestTime) { newestTime = t; newest = f; }
                }
            }
            return newest;
        }
        catch { return null; }
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
        _hookWatcher?.Dispose();
        _pollTimer?.Dispose();
    }
}
