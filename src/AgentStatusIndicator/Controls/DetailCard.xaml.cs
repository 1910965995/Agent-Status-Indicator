using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AgentStatusIndicator.Models;

namespace AgentStatusIndicator.Controls;

public partial class DetailCard : UserControl
{
    public static readonly DependencyProperty StatusColorProperty =
        DependencyProperty.Register(nameof(StatusColor), typeof(Brush), typeof(DetailCard),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x4c, 0xaf, 0x50))));

    public static readonly DependencyProperty StatusTextProperty =
        DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(DetailCard),
            new PropertyMetadata("空闲中"));

    public static readonly DependencyProperty TaskTextProperty =
        DependencyProperty.Register(nameof(TaskText), typeof(string), typeof(DetailCard),
            new PropertyMetadata("等待任务..."));

    public static readonly DependencyProperty ElapsedTimeProperty =
        DependencyProperty.Register(nameof(ElapsedTime), typeof(string), typeof(DetailCard),
            new PropertyMetadata("00:00:00"));

    public Brush StatusColor
    {
        get => (Brush)GetValue(StatusColorProperty);
        set => SetValue(StatusColorProperty, value);
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public string TaskText
    {
        get => (string)GetValue(TaskTextProperty);
        set => SetValue(TaskTextProperty, value);
    }

    public string ElapsedTime
    {
        get => (string)GetValue(ElapsedTimeProperty);
        set => SetValue(ElapsedTimeProperty, value);
    }

    private DateTime? _startedAt;
    private DispatcherTimer? _timer;

    public event EventHandler? ExitRequested;

    public DetailCard()
    {
        InitializeComponent();
    }

    public void UpdateStatus(AgentStatus status, string? task, DateTime? startedAt)
    {
        (StatusColor, StatusText) = status switch
        {
            AgentStatus.Idle => (new SolidColorBrush(Color.FromRgb(0x4c, 0xaf, 0x50)), "空闲中"),
            AgentStatus.Running => (new SolidColorBrush(Color.FromRgb(0xff, 0xc1, 0x07)), "运行中"),
            AgentStatus.Completed => (new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xf3)), "已完成"),
            AgentStatus.Error => (new SolidColorBrush(Color.FromRgb(0xf4, 0x43, 0x36)), "出错"),
            _ => (new SolidColorBrush(Color.FromRgb(0x4c, 0xaf, 0x50)), "空闲中")
        };

        TaskText = task ?? (status == AgentStatus.Idle ? "等待任务..." : "");

        _startedAt = startedAt;
        if (status == AgentStatus.Running && startedAt.HasValue)
        {
            _timer?.Stop();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) => UpdateElapsedTime();
            _timer.Start();
            UpdateElapsedTime();
        }
        else if (status != AgentStatus.Running)
        {
            _timer?.Stop();
            _timer = null;
            if (startedAt.HasValue)
            {
                var elapsed = DateTime.Now - startedAt.Value;
                ElapsedTime = $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
            }
        }
    }

    private void UpdateElapsedTime()
    {
        if (!_startedAt.HasValue) return;
        var elapsed = DateTime.Now - _startedAt.Value;
        ElapsedTime = $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
    }

    public void Show() => Visibility = Visibility.Visible;
    public void Hide() => Visibility = Visibility.Collapsed;
    public bool IsCardVisible => Visibility == Visibility.Visible;

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }
}
