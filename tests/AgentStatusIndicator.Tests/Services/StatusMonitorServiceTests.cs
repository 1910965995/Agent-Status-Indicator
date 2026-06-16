using AgentStatusIndicator.Models;
using AgentStatusIndicator.Services;
using Xunit;

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
