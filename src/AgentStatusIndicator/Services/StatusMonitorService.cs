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

        // If session is currently active, keep showing running
        if (IsSessionActive())
        {
            Notify(AgentStatus.Running, _hookTask, _hookStartedAt);
            return;
        }

        // No session activity → respect hook status
        Notify(_hookStatus, _hookTask, _hookStartedAt);
    }

    private void OnPollTick(object? state)
    {
        // Re-find session file if lost (handles new sessions)
        if (_sessionFilePath == null || !File.Exists(_sessionFilePath))
            _sessionFilePath = FindNewestSessionFile();

        // Update session file modification time
        if (_sessionFilePath != null && File.Exists(_sessionFilePath))
        {
            try
            {
                var sessionWrite = File.GetLastWriteTime(_sessionFilePath);
                if (sessionWrite > _lastSessionWrite)
                    _lastSessionWrite = sessionWrite;
            }
            catch { }
        }

        // Priority 1: Session file active within 3s → Claude is working NOW
        if (IsSessionRecentlyActive())
        {
            if (_lastNotifiedStatus != AgentStatus.Running)
                Notify(AgentStatus.Running, "Claude 工作中...", null);
            return;
        }

        // Priority 2: Session active within 5s → still recently active
        if (IsSessionActive())
        {
            if (_lastNotifiedStatus == AgentStatus.Running) return; // already showing running
            // Let hook decide
        }

        // Priority 3: Hook completed/error (only when session is quiet)
        if (_hookStatus == AgentStatus.Completed)
        {
            if (_lastNotifiedStatus != AgentStatus.Completed)
                Notify(AgentStatus.Completed, _hookTask, _hookStartedAt);
            return;
        }

        if (_hookStatus == AgentStatus.Error)
        {
            if (_lastNotifiedStatus != AgentStatus.Error)
                Notify(AgentStatus.Error, _hookTask, _hookStartedAt);
            return;
        }

        // Priority 4: Hook running (session just went idle, keep running briefly)
        if (_hookStatus == AgentStatus.Running)
        {
            if (_lastNotifiedStatus != AgentStatus.Running)
                Notify(AgentStatus.Running, _hookTask, _hookStartedAt);
            return;
        }

        // Priority 5: Nothing active → idle
        if (_lastNotifiedStatus != AgentStatus.Idle)
            Notify(AgentStatus.Idle, null, null);
    }

    private bool IsSessionRecentlyActive() =>
        _sessionFilePath != null &&
        File.Exists(_sessionFilePath) &&
        (DateTime.Now - File.GetLastWriteTime(_sessionFilePath)) < TimeSpan.FromSeconds(3);

    private bool IsSessionActive() =>
        _sessionFilePath != null &&
        File.Exists(_sessionFilePath) &&
        (DateTime.Now - File.GetLastWriteTime(_sessionFilePath)) < TimeSpan.FromSeconds(5);

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

            // Try project config first (set by init-status.sh)
            var configFile = Path.Combine(homeDir, ".claude", "agent-status", "project.json");
            if (File.Exists(configFile))
            {
                try
                {
                    var configJson = File.ReadAllText(configFile);
                    var config = System.Text.Json.JsonSerializer.Deserialize<ProjectConfig>(configJson);
                    if (config != null && !string.IsNullOrEmpty(config.EncodedName))
                    {
                        var projectDir = Path.Combine(projectsDir, config.EncodedName);
                        if (Directory.Exists(projectDir))
                        {
                            string? newest = null;
                            var newestTime = DateTime.MinValue;
                            foreach (var f in Directory.GetFiles(projectDir, "*.jsonl"))
                            {
                                var t = File.GetLastWriteTime(f);
                                if (t > newestTime) { newestTime = t; newest = f; }
                            }
                            if (newest != null) return newest;
                        }
                    }
                }
                catch { /* fall through */ }
            }

            // Fallback: search all project directories
            string? fallback = null;
            var fallbackTime = DateTime.MinValue;
            foreach (var dir in Directory.GetDirectories(projectsDir))
            {
                foreach (var f in Directory.GetFiles(dir, "*.jsonl"))
                {
                    var t = File.GetLastWriteTime(f);
                    if (t > fallbackTime) { fallbackTime = t; fallback = f; }
                }
            }
            return fallback;
        }
        catch { return null; }
    }

    private record ProjectConfig
    {
        public string? ProjectDir { get; init; }
        public string? EncodedName { get; init; }
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
