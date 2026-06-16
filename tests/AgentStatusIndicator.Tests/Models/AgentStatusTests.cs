using AgentStatusIndicator.Models;
using System.Text.Json;
using Xunit;

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
