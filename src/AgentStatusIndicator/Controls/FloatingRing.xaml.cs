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

    public static readonly DependencyProperty RingStrokeThicknessProperty =
        DependencyProperty.Register(nameof(RingStrokeThickness), typeof(double), typeof(FloatingRing),
            new PropertyMetadata(4.0));

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

    public double RingStrokeThickness
    {
        get => (double)GetValue(RingStrokeThicknessProperty);
        set => SetValue(RingStrokeThicknessProperty, value);
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
        RingGlow.Color = color;  // glow matches ring color
    }

    private void StartBreathing(AgentStatus status)
    {
        _currentAnimation?.Stop();
        _currentAnimation = null;

        // Parameters: (halfCycle, scaleMax, glowBlur, glowOpacity, ringOpacityMin)
        var (halfCycle, scaleMax, glowMax, glowOpacity, opacityMin) = status switch
        {
            AgentStatus.Idle     => (TimeSpan.FromSeconds(1.5), 1.10, 0.0,  0.0, 0.5),
            AgentStatus.Running  => (TimeSpan.FromSeconds(0.75), 1.20, 20.0, 0.5, 0.4),
            AgentStatus.Completed => (TimeSpan.FromSeconds(1.0), 1.14, 16.0, 0.4, 0.5),
            AgentStatus.Error    => (TimeSpan.FromSeconds(0.5), 1.18, 18.0, 0.5, 0.4),
            _ => (TimeSpan.FromSeconds(1.5), 1.10, 0.0, 0.0, 0.5)
        };

        var sb = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };

        // Scale X animation
        var scaleX = new DoubleAnimation(1.0, scaleMax, halfCycle) { AutoReverse = true };
        Storyboard.SetTarget(scaleX, RingScale);
        Storyboard.SetTargetProperty(scaleX, new PropertyPath(ScaleTransform.ScaleXProperty));
        sb.Children.Add(scaleX);

        // Scale Y animation (same as X for uniform scaling)
        var scaleY = new DoubleAnimation(1.0, scaleMax, halfCycle) { AutoReverse = true };
        Storyboard.SetTarget(scaleY, RingScale);
        Storyboard.SetTargetProperty(scaleY, new PropertyPath(ScaleTransform.ScaleYProperty));
        sb.Children.Add(scaleY);

        // Ring opacity animation (the "breathing" brightness)
        var opacity = new DoubleAnimation(1.0, opacityMin, halfCycle) { AutoReverse = true };
        Storyboard.SetTargetProperty(opacity, new PropertyPath(UIElement.OpacityProperty));
        sb.Children.Add(opacity);

        // Glow BlurRadius animation
        if (glowMax > 0)
        {
            var glow = new DoubleAnimation(0, glowMax, halfCycle) { AutoReverse = true };
            Storyboard.SetTarget(glow, RingGlow);
            Storyboard.SetTargetProperty(glow, new PropertyPath(DropShadowEffect.BlurRadiusProperty));
            sb.Children.Add(glow);

            var glowOp = new DoubleAnimation(0, glowOpacity, halfCycle) { AutoReverse = true };
            Storyboard.SetTarget(glowOp, RingGlow);
            Storyboard.SetTargetProperty(glowOp, new PropertyPath(DropShadowEffect.OpacityProperty));
            sb.Children.Add(glowOp);
        }

        _currentAnimation = sb;
        sb.Begin(this);
    }
}
