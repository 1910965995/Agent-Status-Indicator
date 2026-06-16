using AgentStatusIndicator.Converters;
using AgentStatusIndicator.Models;
using System.Windows.Media;
using Xunit;

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
