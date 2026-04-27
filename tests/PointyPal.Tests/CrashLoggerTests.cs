using System;
using System.IO;
using FluentAssertions;
using PointyPal.Infrastructure;
using Xunit;

namespace PointyPal.Tests;

public class CrashLoggerTests : IDisposable
{
    private readonly string _logDir;
    private readonly AppConfig _config;

    public CrashLoggerTests()
    {
        _logDir = Path.Combine(Path.GetTempPath(), "PointyPalCrashTests_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(_logDir);
        _config = new AppConfig { CrashLoggingEnabled = true };
    }

    public void Dispose()
    {
        if (Directory.Exists(_logDir))
        {
            Directory.Delete(_logDir, true);
        }
    }

    [Fact]
    public void HandleCrash_WritesRedactedLog()
    {
        var configService = new ConfigService(Path.Combine(_logDir, "config.json"));
        configService.SaveConfig(_config);
        var appLog = new AppLogService(_config, _logDir);
        var crashLogger = new CrashLogger(
            configService,
            _logDir,
            appLog,
            () => "TestState",
            () => "Timeline123");

        Exception ex;
        try
        {
            throw new InvalidOperationException("API key sk-1234567890abcdef1234567890abcdef failed");
        }
        catch (Exception e)
        {
            ex = e;
        }

        crashLogger.WriteCrashLog(ex, "TestContext");

        string[] files = Directory.GetFiles(_logDir, "crash-*.log");
        files.Should().ContainSingle();

        string logContent = File.ReadAllText(files[0]);
        logContent.Should().Contain("TestContext");
        logContent.Should().Contain("TestState");
        logContent.Should().Contain("Timeline123");
        logContent.Should().Contain("API key [REDACTED_SECRET] failed");
        logContent.Should().NotContain("sk-1234567890abcdef1234567890abcdef");
    }

    [Fact]
    public void HandleCrash_RespectsConfig()
    {
        _config.CrashLoggingEnabled = false;
        var configService = new ConfigService(Path.Combine(_logDir, "config.json"));
        configService.SaveConfig(_config);
        var appLog = new AppLogService(_config, _logDir);
        var crashLogger = new CrashLogger(
            configService,
            _logDir,
            appLog,
            () => "TestState",
            () => "Timeline123");

        crashLogger.WriteCrashLog(new Exception("Test"), "Context");

        string[] files = Directory.GetFiles(_logDir, "crash-*.log");
        files.Should().BeEmpty();
    }
}
