using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AgentStatusIndicator.Controls;
using AgentStatusIndicator.Models;
using AgentStatusIndicator.Services;

namespace AgentStatusIndicator;

public partial class MainWindow : Window
{
    private readonly StatusMonitorService _monitor;
    private bool _isDragging;
    private Point _dragStartPoint;
    private DispatcherTimer? _autoHideTimer;

    public MainWindow()
    {
        InitializeComponent();

        var config = LoadConfig();
        var statusFilePath = ResolvePath(config.StatusFilePath);

        _monitor = new StatusMonitorService(statusFilePath);
        _monitor.StatusChanged += OnStatusChanged;
        _monitor.Start();

        Loaded += (_, _) =>
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 20;
            Top = workArea.Bottom - Height - 60;
        };
    }

    private static AppConfig LoadConfig()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        if (!File.Exists(configPath))
            return new AppConfig();

        try
        {
            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    private static string ResolvePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        return Environment.ExpandEnvironmentVariables(path);
    }

    private void OnRingClick(object sender, MouseButtonEventArgs e)
    {
        CardToggle.IsChecked = !CardToggle.IsChecked;
        e.Handled = true;
    }

    private void OnStatusChanged(object? sender, StatusChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            StatusRing.Status = e.Status;
            StatusCard.UpdateStatus(e.Status, e.Task, e.StartedAt);
            SetupAutoHide(e.Status);
        });
    }

    private void SetupAutoHide(AgentStatus status)
    {
        _autoHideTimer?.Stop();
        _autoHideTimer = null;

        int delaySeconds = status switch
        {
            AgentStatus.Completed => 10,
            AgentStatus.Error => 60,
            _ => 0
        };

        if (delaySeconds > 0)
        {
            _autoHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(delaySeconds)
            };
            _autoHideTimer.Tick += (_, _) =>
            {
                _autoHideTimer?.Stop();
                StatusRing.Status = AgentStatus.Idle;
                StatusCard.UpdateStatus(AgentStatus.Idle, null, null);
            };
            _autoHideTimer.Start();
        }
    }

    private void OnExitRequested(object? sender, System.EventArgs e)
    {
        Application.Current.Shutdown();
    }

    // Window dragging
    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(this);
            CardToggle.IsChecked = false; // hide popup while dragging
        }
    }

    private void OnWindowMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
    }

    private void OnWindowMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        var currentPos = e.GetPosition(this);
        var offset = currentPos - _dragStartPoint;

        if (Math.Abs(offset.X) > 3 || Math.Abs(offset.Y) > 3)
        {
            Left += offset.X;
            Top += offset.Y;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _monitor.Dispose();
        _autoHideTimer?.Stop();
        base.OnClosed(e);
    }
}
