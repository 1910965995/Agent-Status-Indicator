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
