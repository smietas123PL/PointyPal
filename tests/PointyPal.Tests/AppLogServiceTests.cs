using System;
using System.IO;
using System.Threading;
using FluentAssertions;
using PointyPal.Infrastructure;
using Xunit;

namespace PointyPal.Tests;

public class AppLogServiceTests : IDisposable
{
    private readonly string _logDir;
    private readonly AppConfig _config;

    public AppLogServiceTests()
    {
        _logDir = Path.Combine(Path.GetTempPath(), "PointyPalTests_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(_logDir);
        _config = new AppConfig { AppLoggingEnabled = true, LogLevel = "Debug" };
    }

    public void Dispose()
    {
        if (Directory.Exists(_logDir))
        {
            Directory.Delete(_logDir, true);
        }
    }

    [Theory]
    [InlineData("Bearer sk-1234567890abcdef1234567890abcdef", "Bearer [REDACTED_SECRET]")]
    [InlineData("My api_key=sk-something-secret123", "My api_key=[REDACTED_SECRET]")]
    [InlineData("header: Authorization: Bearer XYZ123", "header: Authorization: [REDACTED_SECRET] [REDACTED_SECRET]")]
    [InlineData("x-api-key: some_value", "x-api-key: [REDACTED_SECRET]")]
    [InlineData("password = 'my_secret_password'", "password = [REDACTED_SECRET]")]
    public void Redact_Secrets_ReplacesWithRedactedSecret(string input, string expected)
    {
        AppLogService.Redact(input).Should().Be(expected);
    }

    [Fact]
    public void Redact_Base64_ReplacesWithRedactedBase64()
    {
        string base64 = new string('A', 200); // long enough to trigger base64 redaction
        string input = $"AudioBase64=\"{base64}\"";
        string expected = "AudioBase64=\"[REDACTED_BASE64]\"";
        
        AppLogService.Redact(input).Should().Be(expected);
    }

    [Fact]
    public void Redact_LongBase64WithoutPrefix_ReplacesWithRedactedBase64()
    {
        string base64 = new string('A', 200); // long enough
        string input = $"some random {base64} text";
        string expected = "some random [REDACTED_BASE64] text";
        
        AppLogService.Redact(input).Should().Be(expected);
    }

    [Fact]
    public void Log_RespectsLogLevel()
    {
        _config.LogLevel = "Error";
        var logger = new AppLogService(_config, _logDir);
        
        logger.Info("ShouldNotLog", "Info message");
        logger.Error("ShouldLog", "Error message");

        string[] lines = File.ReadAllLines(logger.LogPath);
        lines.Should().ContainSingle();
        lines[0].Should().Contain("[Error] ShouldLog | Error message");
    }
}
