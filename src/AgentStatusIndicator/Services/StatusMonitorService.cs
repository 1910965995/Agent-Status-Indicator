using System.IO;
using System.Text.Json;
using AgentStatusIndicator.Models;

namespace AgentStatusIndicator.Services;

public class StatusMonitorService : IDisposable
{
    private readonly string _filePath;
    private readonly string _watchDir;
    private FileSystemWatcher? _hookWatcher;
    private FileSystemWatcher? _sessionWatcher;
    private Timer? _sessionTimer;
    private bool _disposed;
    private AgentStatus _hookStatus = AgentStatus.Idle;
    private DateTime _lastSessionActivity = DateTime.MinValue;
    private static readonly TimeSpan SessionActiveWindow = TimeSpan.FromSeconds(3);
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
        // Read initial state from hook file
        var initial = ReadCurrentStatus();
        if (initial != null)
        {
            var status = ParseAgentStatus(initial.Status);
            _hookStatus = status;
            NotifyStatusChanged(status, initial.Task, ParseDateTime(initial.StartedAt));
        }

        // Watch the hook status file
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

        // Watch the Claude session directory for activity
        var sessionDir = FindClaudeSessionDir();
        if (sessionDir != null && Directory.Exists(sessionDir))
        {
            _sessionWatcher = new FileSystemWatcher(sessionDir)
            {
                Filter = "*.jsonl",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false
            };
            _sessionWatcher.Changed += OnSessionActivity;
            _sessionWatcher.Created += OnSessionActivity;
            _sessionWatcher.EnableRaisingEvents = true;
        }

        // Periodic check: if session was recently active, ensure status is "running"
        _sessionTimer = new Timer(OnSessionTimerTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
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

    private void OnHookFileChanged(object sender, FileSystemEventArgs e)
    {
        Thread.Sleep(100);
        var data = ReadCurrentStatus();
        if (data == null) return;

        var status = ParseAgentStatus(data.Status);
        _hookStatus = status;

        // Hook says completed/error → respect it, override session activity
        if (status == AgentStatus.Completed || status == AgentStatus.Error)
        {
            NotifyStatusChanged(status, data.Task, ParseDateTime(data.StartedAt));
        }
        // Hook says running → show running
        else if (status == AgentStatus.Running)
        {
            NotifyStatusChanged(AgentStatus.Running, data.Task, ParseDateTime(data.StartedAt));
        }
        // Hook says idle → let session timer decide if we're actually idle
        else
        {
            CheckSessionIdle();
        }
    }

    private void OnSessionActivity(object sender, FileSystemEventArgs e)
    {
        // Session file changed → Claude is active (thinking, writing, using tools)
        _lastSessionActivity = DateTime.Now;

        // If hook hasn't set a specific status, override to running
        if (_hookStatus == AgentStatus.Idle)
        {
            NotifyStatusChanged(AgentStatus.Running, "Claude 工作中...", null);
        }
    }

    private void OnSessionTimerTick(object? state)
    {
        CheckSessionIdle();
    }

    private void CheckSessionIdle()
    {
        // If hook says running/completed/error, don't override
        if (_hookStatus != AgentStatus.Idle) return;

        // If session was recently active, keep showing running
        if (DateTime.Now - _lastSessionActivity < SessionIdleTimeout)
        {
            NotifyStatusChanged(AgentStatus.Running, "Claude 工作中...", null);
            return;
        }

        // Truly idle
        NotifyStatusChanged(AgentStatus.Idle, null, null);
    }

    /// <summary>
    /// Find the Claude session directory for the current project.
    /// Looks for the most recently modified directory in ~/.claude/projects/
    /// that matches the current working directory.
    /// </summary>
    private static string? FindClaudeSessionDir()
    {
        try
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var projectsDir = Path.Combine(homeDir, ".claude", "projects");

            if (!Directory.Exists(projectsDir)) return null;

            // Find the newest .jsonl file across all project directories
            string? newestFile = null;
            var newestTime = DateTime.MinValue;

            foreach (var dir in Directory.GetDirectories(projectsDir))
            {
                foreach (var file in Directory.GetFiles(dir, "*.jsonl"))
                {
                    var lastWrite = File.GetLastWriteTime(file);
                    if (lastWrite > newestTime)
                    {
                        newestTime = lastWrite;
                        newestFile = file;
                    }
                }
            }

            if (newestFile != null)
                return Path.GetDirectoryName(newestFile);
        }
        catch
        {
            // Permission errors etc — just skip
        }

        return null;
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
        _hookWatcher?.Dispose();
        _sessionWatcher?.Dispose();
        _sessionTimer?.Dispose();
    }
}
