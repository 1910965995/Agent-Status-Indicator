using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
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

        var scaleAnim = new DoubleAnimation(1.0, scaleMax, halfDuration)
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };

        var glowAnim = new DoubleAnimation(0, glowMax, halfDuration)
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };

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
