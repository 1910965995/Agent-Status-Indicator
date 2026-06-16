# Claude Code CLI Status Indicator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows desktop floating ring indicator that shows Claude Code CLI running status via breathing animation.

**Architecture:** WPF (.NET 10) transparent overlay window with a 40px breath-animated ring. Status detected via Claude Code hooks that write to a shared `status.json`, monitored by `FileSystemWatcher`. A clickable detail card shows task info, elapsed time, and exit button.

**Tech Stack:** .NET 10 / WPF, C# 13, System.Text.Json, Shell Script (bash), xUnit (testing)

---

### Task 1: Project scaffolding + hooks

**Files:**
- Create: `src/hooks/status-writer.sh`
- Create: `src/AgentStatusIndicator/AgentStatusIndicator.csproj`
- Create: `src/AgentStatusIndicator/App.xaml`
- Create: `src/AgentStatusIndicator/App.xaml.cs`

- [ ] **Step 1: Create hooks/status-writer.sh**

```bash
#!/bin/bash
# status-writer.sh — Called by Claude Code hooks to write current status
# Usage: status-writer.sh <status> [message]

STATUS_DIR="$HOME/.claude/agent-status"
STATUS_FILE="$STATUS_DIR/status.json"
STATUS="${1:-idle}"
MESSAGE="${2:-}"

mkdir -p "$STATUS_DIR"

cat > "$STATUS_FILE" << EOF
{
  "status": "$STATUS",
  "task": "$MESSAGE",
  "started_at": "$(date -Iseconds)"
}
EOF
```

- [ ] **Step 2: Create .csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <RootNamespace>AgentStatusIndicator</RootNamespace>
    <AssemblyName>AgentStatusIndicator</AssemblyName>
    <SelfContained>true</SelfContained>
  </PropertyGroup>

</Project>
```

- [ ] **Step 3: Create App.xaml**

```xml
<Application x:Class="AgentStatusIndicator.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
    <Application.Resources>
        <ResourceDictionary />
    </Application.Resources>
</Application>
```

- [ ] **Step 4: Create App.xaml.cs**

```csharp
using System.Windows;

namespace AgentStatusIndicator;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
    }
}
```

- [ ] **Step 5: Commit**

```bash
git add src/hooks/status-writer.sh src/AgentStatusIndicator/AgentStatusIndicator.csproj src/AgentStatusIndicator/App.xaml src/AgentStatusIndicator/App.xaml.cs
git commit -m "feat: scaffold project with hooks and WPF entry point"
```

---

### Task 2: Models + Status-to-Color converter (TDD)
**Files:**
- Create: `src/AgentStatusIndicator/Models/AgentStatus.cs`
- Create: `src/AgentStatusIndicator/Models/AppConfig.cs`
- Create: `src/AgentStatusIndicator/Converters/StatusToColorConverter.cs`
- Create: `tests/AgentStatusIndicator.Tests/AgentStatusIndicator.Tests.csproj`
- Create: `tests/AgentStatusIndicator.Tests/Models/AgentStatusTests.cs`
- Create: `tests/AgentStatusIndicator.Tests/Converters/StatusToColorConverterTests.cs`

- [ ] **Step 1: Write the failing AgentStatus model tests**

```csharp
using AgentStatusIndicator.Models;
using System.Text.Json;

namespace AgentStatusIndicator.Tests.Models;

public class AgentStatusTests
{
    [Theory]
    [InlineData("idle", AgentStatus.Idle)]
    [InlineData("running", AgentStatus.Running)]
    [InlineData("completed", AgentStatus.Completed)]
    [InlineData("error", AgentStatus.Error)]
    public void ParseStatus_FromString_ReturnsCorrectEnum(string str, AgentStatus expected)
    {
        var result = Enum.Parse<AgentStatus>(str, ignoreCase: true);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void StatusFileData_Deserializes_FromValidJson()
    {
        var json = """{"status":"running","task":"refactor function","started_at":"2026-06-16T09:00:00+08:00"}""";
        var data = JsonSerializer.Deserialize<StatusFileData>(json);
        Assert.NotNull(data);
        Assert.Equal("running", data.Status);
        Assert.Equal("refactor function", data.Task);
        Assert.Equal("2026-06-16T09:00:00+08:00", data.StartedAt);
    }

    [Fact]
    public void StatusFileData_Deserializes_WithMissingOptionalFields()
    {
        var json = """{"status":"idle"}""";
        var data = JsonSerializer.Deserialize<StatusFileData>(json);
        Assert.NotNull(data);
        Assert.Equal("idle", data.Status);
        Assert.Null(data.Task);
        Assert.Null(data.StartedAt);
    }

    [Fact]
    public void StatusFileData_Deserializes_InvalidStatus_StillParses()
    {
        var json = """{"status":"unknown_status"}""";
        var data = JsonSerializer.Deserialize<StatusFileData>(json);
        Assert.NotNull(data);
        Assert.Equal("unknown_status", data.Status);
    }
}
```

- [ ] **Step 2: Write the failing converter tests**

```csharp
using AgentStatusIndicator.Converters;
using AgentStatusIndicator.Models;
using System.Windows.Media;

namespace AgentStatusIndicator.Tests.Converters;

public class StatusToColorConverterTests
{
    private readonly StatusToColorConverter _converter = new();

    [Theory]
    [InlineData(AgentStatus.Idle, "#FF4CAF50")]
    [InlineData(AgentStatus.Running, "#FFFFC107")]
    [InlineData(AgentStatus.Completed, "#FF2196F3")]
    [InlineData(AgentStatus.Error, "#FFF44336")]
    public void Convert_Status_ReturnsCorrectColor(AgentStatus status, string expectedHex)
    {
        var result = _converter.Convert(status, typeof(Color), null, null);
        var expected = ColorConverter.ConvertFromString(expectedHex);
        Assert.Equal(expected, result);
    }
}
```

- [ ] **Step 3: Create the test project .csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\AgentStatusIndicator\AgentStatusIndicator.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 4: Run tests to confirm they fail**

Run: `dotnet test tests/AgentStatusIndicator.Tests --no-restore 2>&1 || echo "Expected failure - no source yet"`
Expected: Build fails — `AgentStatus` type not found.

- [ ] **Step 5: Create Model and Converter source files**

`src/AgentStatusIndicator/Models/AgentStatus.cs`:
```csharp
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
    public string Status { get; init; } = "idle";
    public string? Task { get; init; }
    public string? StartedAt { get; init; }
}
```

`src/AgentStatusIndicator/Models/AppConfig.cs`:
```csharp
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
```

`src/AgentStatusIndicator/Converters/StatusToColorConverter.cs`:
```csharp
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AgentStatusIndicator.Models;

namespace AgentStatusIndicator.Converters;

[ValueConversion(typeof(AgentStatus), typeof(Color))]
public class StatusToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AgentStatus status)
        {
            return status switch
            {
                AgentStatus.Idle => Color.FromRgb(0x4c, 0xaf, 0x50),
                AgentStatus.Running => Color.FromRgb(0xff, 0xc1, 0x07),
                AgentStatus.Completed => Color.FromRgb(0x21, 0x96, 0xf3),
                AgentStatus.Error => Color.FromRgb(0xf4, 0x43, 0x36),
                _ => Colors.Gray
            };
        }
        return Colors.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
```

- [ ] **Step 6: Run tests to confirm they pass**

Run: `dotnet test tests/AgentStatusIndicator.Tests -v n`
Expected: All 6 tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/AgentStatusIndicator/Models/ src/AgentStatusIndicator/Converters/ tests/
git commit -m "feat: add status models and color converter with tests"
```

---

### Task 3: StatusMonitorService (TDD)

**Files:**
- Create: `src/AgentStatusIndicator/Services/StatusMonitorService.cs`
- Create: `src/AgentStatusIndicator/Services/StatusChangedEventArgs.cs`
- Create: `tests/AgentStatusIndicator.Tests/Services/StatusMonitorServiceTests.cs`

- [ ] **Step 1: Write the StatusMonitorService tests**

```csharp
using AgentStatusIndicator.Models;
using AgentStatusIndicator.Services;

namespace AgentStatusIndicator.Tests.Services;

public class StatusMonitorServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testFile;
    private readonly StatusMonitorService _service;

    public StatusMonitorServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "AgentStatusTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _testFile = Path.Combine(_testDir, "status.json");
        _service = new StatusMonitorService(_testFile);
    }

    [Fact]
    public void ReadCurrentStatus_FileNotExists_ReturnsNull()
    {
        var result = _service.ReadCurrentStatus();
        Assert.Null(result);
    }

    [Fact]
    public void ReadCurrentStatus_ValidFile_ReturnsParsedData()
    {
        File.WriteAllText(_testFile, """{"status":"running","task":"test task"}""");
        var result = _service.ReadCurrentStatus();
        Assert.NotNull(result);
        Assert.Equal("running", result.Status);
        Assert.Equal("test task", result.Task);
    }

    [Fact]
    public void ReadCurrentStatus_InvalidJson_ReturnsNull()
    {
        File.WriteAllText(_testFile, "not valid json");
        var result = _service.ReadCurrentStatus();
        Assert.Null(result);
    }

    [Fact]
    public void ReadCurrentStatus_EmptyFile_ReturnsNull()
    {
        File.WriteAllText(_testFile, "");
        var result = _service.ReadCurrentStatus();
        Assert.Null(result);
    }

    public void Dispose()
    {
        _service.Dispose();
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }
}
```

- [ ] **Step 2: Run tests — expected failure**

Run: `dotnet test tests/AgentStatusIndicator.Tests --filter "FullyQualifiedName~StatusMonitorService" -v n`
Expected: Build fails — `StatusMonitorService` not found.

- [ ] **Step 3: Write StatusChangedEventArgs**

```csharp
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
```

- [ ] **Step 4: Write StatusMonitorService**

```csharp
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
```

- [ ] **Step 5: Run tests — expected pass**

Run: `dotnet test tests/AgentStatusIndicator.Tests --filter "FullyQualifiedName~StatusMonitorService" -v n`
Expected: All 4 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/AgentStatusIndicator/Services/ tests/AgentStatusIndicator.Tests/Services/
git commit -m "feat: add StatusMonitorService with file monitoring and tests"
```

---

### Task 4: FloatingRing control

**Files:**
- Create: `src/AgentStatusIndicator/Controls/FloatingRing.xaml`
- Create: `src/AgentStatusIndicator/Controls/FloatingRing.xaml.cs`

- [ ] **Step 1: Write FloatingRing.xaml**

```xml
<UserControl x:Class="AgentStatusIndicator.Controls.FloatingRing"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Name="RingControl">
    <Grid>
        <Ellipse x:Name="Ring"
                 Width="{Binding ElementName=RingControl, Path=RingDiameter}"
                 Height="{Binding ElementName=RingControl, Path=RingDiameter}"
                 Stroke="{Binding ElementName=RingControl, Path=RingBrush}"
                 StrokeThickness="{Binding ElementName=RingControl, Path=StrokeThickness}"
                 RenderTransformOrigin="0.5,0.5">
            <Ellipse.RenderTransform>
                <ScaleTransform x:Name="RingScale" ScaleX="1" ScaleY="1" />
            </Ellipse.RenderTransform>
            <Ellipse.Effect>
                <DropShadowEffect x:Name="RingGlow"
                                  BlurRadius="0"
                                  ShadowDepth="0"
                                  Opacity="0" />
            </Ellipse.Effect>
        </Ellipse>
    </Grid>
</UserControl>
```

- [ ] **Step 2: Write FloatingRing.xaml.cs**

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AgentStatusIndicator.Models;

namespace AgentStatusIndicator.Controls;

public partial class FloatingRing : UserControl
{
    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(AgentStatus), typeof(FloatingRing),
            new PropertyMetadata(AgentStatus.Idle, OnStatusChanged));

    public static readonly DependencyProperty RingDiameterProperty =
        DependencyProperty.Register(nameof(RingDiameter), typeof(double), typeof(FloatingRing),
            new PropertyMetadata(40.0));

    public AgentStatus Status
    {
        get => (AgentStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public double RingDiameter
    {
        get => (double)GetValue(RingDiameterProperty);
        set => SetValue(RingDiameterProperty, value);
    }

    public Brush RingBrush { get; private set; } = new SolidColorBrush(Color.FromRgb(0x4c, 0xaf, 0x50));

    private Storyboard? _currentAnimation;

    public FloatingRing()
    {
        InitializeComponent();
        StartBreathing(AgentStatus.Idle);
    }

    private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FloatingRing ring && e.NewValue is AgentStatus status)
        {
            ring.UpdateRingColor(status);
            ring.StartBreathing(status);
        }
    }

    private void UpdateRingColor(AgentStatus status)
    {
        var color = status switch
        {
            AgentStatus.Idle => Color.FromRgb(0x4c, 0xaf, 0x50),
            AgentStatus.Running => Color.FromRgb(0xff, 0xc1, 0x07),
            AgentStatus.Completed => Color.FromRgb(0x21, 0x96, 0xf3),
            AgentStatus.Error => Color.FromRgb(0xf4, 0x43, 0x36),
            _ => Colors.Gray
        };
        RingBrush = new SolidColorBrush(color);
        Ring.Stroke = RingBrush;
    }

    private void StartBreathing(AgentStatus status)
    {
        // Stop current animation
        _currentAnimation?.Stop();
        _currentAnimation = null;

        var (duration, scaleMax, glowMax, glowOpacity) = status switch
        {
            AgentStatus.Idle => (TimeSpan.FromSeconds(3), 1.05, 0.0, 0.0),
            AgentStatus.Running => (TimeSpan.FromSeconds(1.5), 1.18, 20.0, 0.5),
            AgentStatus.Completed => (TimeSpan.FromSeconds(2), 1.12, 16.0, 0.4),
            AgentStatus.Error => (TimeSpan.FromSeconds(1), 1.15, 18.0, 0.5),
            _ => (TimeSpan.FromSeconds(3), 1.05, 0.0, 0.0)
        };

        var halfDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds / 2);

        // Scale animation
        var scaleAnim = new DoubleAnimation(1.0, scaleMax, halfDuration)
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };

        // Glow animation (BlurRadius)
        var glowAnim = new DoubleAnimation(0, glowMax, halfDuration)
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };

        // Glow opacity animation
        var glowOpacityAnim = new DoubleAnimation(0, glowOpacity, halfDuration)
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };

        var sb = new Storyboard();
        Storyboard.SetTarget(scaleAnim, RingScale);
        Storyboard.SetTargetProperty(scaleAnim, new PropertyPath(ScaleTransform.ScaleXProperty));
        Storyboard.SetTarget(glowAnim, RingGlow);
        Storyboard.SetTargetProperty(glowAnim, new PropertyPath(DropShadowEffect.BlurRadiusProperty));
        Storyboard.SetTarget(glowOpacityAnim, RingGlow);
        Storyboard.SetTargetProperty(glowOpacityAnim, new PropertyPath(DropShadowEffect.OpacityProperty));
        sb.Children.Add(scaleAnim);
        sb.Children.Add(glowAnim);
        sb.Children.Add(glowOpacityAnim);

        _currentAnimation = sb;
        sb.Begin(this);
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add src/AgentStatusIndicator/Controls/FloatingRing.xaml src/AgentStatusIndicator/Controls/FloatingRing.xaml.cs
git commit -m "feat: add FloatingRing control with breathing animations"
```

---

### Task 5: DetailCard control

**Files:**
- Create: `src/AgentStatusIndicator/Controls/DetailCard.xaml`
- Create: `src/AgentStatusIndicator/Controls/DetailCard.xaml.cs`

- [ ] **Step 1: Write DetailCard.xaml**

```xml
<UserControl x:Class="AgentStatusIndicator.Controls.DetailCard"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Name="CardControl"
             Visibility="Collapsed">
    <Border Width="280" Height="160"
            Background="#1E1E1E"
            CornerRadius="12"
            BorderBrush="#333333"
            BorderThickness="1"
            Padding="16">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto" />
                <RowDefinition Height="*" />
                <RowDefinition Height="Auto" />
            </Grid.RowDefinitions>

            <!-- Status row -->
            <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,8">
                <Ellipse Width="14" Height="14"
                         Fill="{Binding ElementName=CardControl, Path=StatusColor}"
                         Margin="0,0,8,0" VerticalAlignment="Center" />
                <TextBlock Text="{Binding ElementName=CardControl, Path=StatusText}"
                           Foreground="White" FontSize="14" FontWeight="Bold"
                           VerticalAlignment="Center" />
            </StackPanel>

            <!-- Task info -->
            <TextBlock Grid.Row="1"
                       Text="{Binding ElementName=CardControl, Path=TaskText}"
                       Foreground="#AAAAAA" FontSize="12"
                       TextWrapping="Wrap"
                       VerticalAlignment="Center" />

            <!-- Bottom: duration + exit button -->
            <Grid Grid.Row="2">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="Auto" />
                </Grid.ColumnDefinitions>

                <StackPanel Grid.Column="0" Orientation="Horizontal" VerticalAlignment="Center">
                    <TextBlock Text="运行时长 " Foreground="#888888" FontSize="12" />
                    <TextBlock Text="{Binding ElementName=CardControl, Path=ElapsedTime}"
                               Foreground="White" FontSize="12"
                               FontFamily="Consolas" />
                </StackPanel>

                <Button Grid.Column="1"
                        Content="退出插件"
                        Width="80" Height="28"
                        Background="#F44336"
                        Foreground="White"
                        BorderThickness="0"
                        FontSize="12"
                        Click="OnExitClick" />
            </Grid>
        </Grid>
    </Border>
</UserControl>
```

- [ ] **Step 2: Write DetailCard.xaml.cs**

```csharp
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
        get => _elapsedTime;
        set
        {
            _elapsedTime = value;
            // Using a simple approach: update via DispatcherTimer
        }
    }

    private string _elapsedTime = "00:00:00";
    private DateTime? _startedAt;
    private DispatcherTimer? _timer;

    public event EventHandler? ExitRequested;

    public DetailCard()
    {
        InitializeComponent();
    }

    public void UpdateStatus(AgentStatus status, string? task, DateTime? startedAt)
    {
        // Update status display
        (StatusColor, StatusText) = status switch
        {
            AgentStatus.Idle => (new SolidColorBrush(Color.FromRgb(0x4c, 0xaf, 0x50)), "空闲中"),
            AgentStatus.Running => (new SolidColorBrush(Color.FromRgb(0xff, 0xc1, 0x07)), "运行中"),
            AgentStatus.Completed => (new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xf3)), "已完成"),
            AgentStatus.Error => (new SolidColorBrush(Color.FromRgb(0xf4, 0x43, 0x36)), "出错"),
            _ => (new SolidColorBrush(Color.FromRgb(0x4c, 0xaf, 0x50)), "空闲中")
        };

        TaskText = task ?? (status == AgentStatus.Idle ? "等待任务..." : "");

        // Handle timer
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
            if (startedAt.HasValue && status == AgentStatus.Completed)
            {
                // Show final elapsed time
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
```

- [ ] **Step 3: Commit**

```bash
git add src/AgentStatusIndicator/Controls/DetailCard.xaml src/AgentStatusIndicator/Controls/DetailCard.xaml.cs
git commit -m "feat: add DetailCard control with status info and elapsed timer"
```

---

### Task 6: MainWindow (integration)

**Files:**
- Create: `src/AgentStatusIndicator/MainWindow.xaml`
- Create: `src/AgentStatusIndicator/MainWindow.xaml.cs`

- [ ] **Step 1: Write MainWindow.xaml**

```xml
<Window x:Class="AgentStatusIndicator.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:controls="clr-namespace:AgentStatusIndicator.Controls"
        xmlns:models="clr-namespace:AgentStatusIndicator.Models"
        Title="Agent Status Indicator"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        ResizeMode="NoResize"
        Width="48" Height="48"
        WindowStartupLocation="Manual"
        Left="{Binding Source={x:Static SystemParameters.WorkArea}, Path=Right}"
        Top="100"
        MouseDown="OnWindowMouseDown"
        MouseLeftButtonUp="OnWindowMouseUp"
        MouseMove="OnWindowMouseMove">
    <Grid>
        <!-- Ring control -->
        <controls:FloatingRing x:Name="StatusRing"
                               Width="48" Height="48"
                               HorizontalAlignment="Center"
                               VerticalAlignment="Center"
                               MouseLeftButtonUp="OnRingClick" />
        <!-- Detail card popup -->
        <Popup x:Name="DetailPopup"
               Placement="Relative"
               PlacementTarget="{Binding ElementName=StatusRing}"
               HorizontalOffset="60"
               VerticalOffset="-10"
               StaysOpen="False"
               IsOpen="{Binding ElementName=CardToggle, Path=IsChecked}">
            <Grid>
                <controls:DetailCard x:Name="StatusCard"
                                     ExitRequested="OnExitRequested" />
            </Grid>
        </Popup>
        <!-- Hidden checkbox to drive Popup IsOpen -->
        <CheckBox x:Name="CardToggle" IsChecked="False" Visibility="Collapsed" />
    </Grid>
</Window>
```

- [ ] **Step 2: Write MainWindow.xaml.cs**

```csharp
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
    private AgentStatus _currentStatus = AgentStatus.Idle;
    private DateTime? _statusChangedAt;
    private DispatcherTimer? _autoHideTimer;

    public MainWindow()
    {
        InitializeComponent();

        // Determine status file path
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var statusFilePath = Path.Combine(homeDir, ".claude", "agent-status", "status.json");

        _monitor = new StatusMonitorService(statusFilePath);
        _monitor.StatusChanged += OnStatusChanged;
        _monitor.Start();
    }

    private void OnRingClick(object sender, MouseButtonEventArgs e)
    {
        // Toggle detail card
        CardToggle.IsChecked = !CardToggle.IsChecked;
    }

    private void OnStatusChanged(object? sender, StatusChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _currentStatus = e.Status;
            _statusChangedAt = DateTime.Now;

            StatusRing.Status = e.Status;
            StatusCard.UpdateStatus(e.Status, e.Task, e.StartedAt);

            // Auto-hide logic for completed/error states
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
            // Hide popup while dragging
            CardToggle.IsChecked = false;
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
```

- [ ] **Step 3: Commit**

```bash
git add src/AgentStatusIndicator/MainWindow.xaml src/AgentStatusIndicator/MainWindow.xaml.cs
git commit -m "feat: add MainWindow with drag, popup, and status monitoring"
```

---

### Task 7: Configuration + appsettings.json

**Files:**
- Create: `src/AgentStatusIndicator/appsettings.json`
- Modify: `src/AgentStatusIndicator/MainWindow.xaml.cs` (to load config)

- [ ] **Step 1: Create appsettings.json**

```json
{
  "StatusFilePath": "%USERPROFILE%\\.claude\\agent-status\\status.json",
  "RingSize": 40.0,
  "StrokeThickness": 4.0,
  "TimeoutSeconds": 10,
  "CompletedHoldSeconds": 10,
  "ErrorHoldSeconds": 60,
  "Colors": {
    "Idle": "#4caf50",
    "Running": "#ffc107",
    "Completed": "#2196f3",
    "Error": "#f44336"
  }
}
```

Set property: Copy to output directory.

- [ ] **Step 2: Update MainWindow.xaml.cs to load config**

Replace hardcoded path with config resolution:

```csharp
public MainWindow()
{
    InitializeComponent();

    var config = LoadConfig();
    var statusFilePath = ResolvePath(config.StatusFilePath);

    _monitor = new StatusMonitorService(statusFilePath);
    _monitor.StatusChanged += OnStatusChanged;
    _monitor.Start();
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
```

Also add `using System.Text.Json;` and `using AgentStatusIndicator.Models;` at the top.

- [ ] **Step 3: Update .csproj to copy appsettings.json to output**

Add to `AgentStatusIndicator.csproj`:

```xml
  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
```

- [ ] **Step 4: Commit**

```bash
git add src/AgentStatusIndicator/appsettings.json src/AgentStatusIndicator/AgentStatusIndicator.csproj
git commit -m "feat: add configuration file and path resolution"
```

---

### Task 8: Final integration + packaging

**Files:**
- Modify: `src/AgentStatusIndicator/MainWindow.xaml.cs` (set startup position to bottom-right)
- Create: `src/AgentStatusIndicator/app.manifest` (DPI awareness)

- [ ] **Step 1: Set default position to bottom-right**

In `MainWindow.xaml.cs`, add a `Loaded` event handler in constructor:

```csharp
Loaded += (_, _) =>
{
    var workArea = SystemParameters.WorkArea;
    Left = workArea.Right - Width - 20;
    Top = workArea.Bottom - Height - 60;
};
```

- [ ] **Step 2: Verify full build and test**

Run: `dotnet build src/AgentStatusIndicator --configuration Release`
Expected: Build succeeds with no errors.

Run: `dotnet test tests/AgentStatusIndicator.Tests -v n`
Expected: All tests pass.

- [ ] **Step 3: Publish self-contained executable**

Run: `dotnet publish src/AgentStatusIndicator --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/`

Expected: `publish/AgentStatusIndicator.exe` (~30-50MB self-contained) is produced.

- [ ] **Step 4: Commit final changes**

```bash
git add src/AgentStatusIndicator/
git commit -m "feat: finalize integration, set default position, add publishing config"
```

---

### Task 9: Claude Code hooks setup

**Files:**
- Create: `claude.json` (project root)

- [ ] **Step 1: Create claude.json**

```json
{
  "hooks": {
    "beforeTask": "bash src/hooks/status-writer.sh running \"$CLAUDE_TASK\"",
    "afterTask": "bash src/hooks/status-writer.sh completed",
    "onError": "bash src/hooks/status-writer.sh error",
    "onShutdown": "bash src/hooks/status-writer.sh idle"
  }
}
```

Note: The exact hook event names depend on Claude Code's hook API. The file structure above is the intended configuration — adjust event names if Claude Code uses different terminology.

- [ ] **Step 2: Make hooks script executable and test**

Run: `chmod +x src/hooks/status-writer.sh`
Run: `bash src/hooks/status-writer.sh idle "test"`
Expected: Creates `~/.claude/agent-status/status.json` with idle status.

- [ ] **Step 3: Commit**

```bash
git add claude.json
git commit -m "feat: add Claude Code hooks configuration"
```

---

## Verification Checklist

Post-implementation, verify manually:

- [ ] `AgentStatusIndicator.exe` starts and shows a 40px ring in bottom-right corner
- [ ] Ring is transparent, always-on-top, and draggable
- [ ] Clicking ring opens detail card with placeholder info
- [ ] `bash src/hooks/status-writer.sh running "test"` → ring turns yellow with fast breathing
- [ ] `bash src/hooks/status-writer.sh completed` → ring turns blue, 10s later back to green
- [ ] `bash src/hooks/status-writer.sh error` → ring turns red, 60s later back to green
- [ ] Detail card shows correct status text, elapsed time
- [ ] "退出插件" button exits the app
- [ ] Delete status.json → ring stays green (idle)
- [ ] Add invalid JSON to status.json → ring stays on current state
