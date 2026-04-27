using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PointyPal.Infrastructure;

public class DebugLogger
{
    private readonly ConfigService _configService;
    private readonly string _debugDir;

    public DebugLogger(ConfigService configService)
    {
        _configService = configService;
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _debugDir = Path.Combine(appData, "PointyPal", "debug");
    }

    public void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(_debugDir);
            string logPath = Path.Combine(_debugDir, "app.log");
            string entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}{Environment.NewLine}";
            File.AppendAllText(logPath, entry);
            System.Diagnostics.Debug.WriteLine(entry);
        }
        catch { /* ignore */ }
    }

    public static void LogStatic(string message)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string debugDir = Path.Combine(appData, "PointyPal", "debug");
            Directory.CreateDirectory(debugDir);
            string logPath = Path.Combine(debugDir, "app.log");
            string entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [STATIC] - {message}{Environment.NewLine}";
            File.AppendAllText(logPath, entry);
            System.Diagnostics.Debug.WriteLine(entry);
        }
        catch { /* ignore */ }
    }

    public string SaveDebugJson(string fileName, object data)
    {
        if (!_configService.Config.SaveDebugArtifacts) return "disabled";

        try
        {
            Directory.CreateDirectory(_debugDir);
            string path = Path.Combine(_debugDir, fileName);

            string json;
            if (data is string s)
            {
                json = s;
            }
            else
            {
                json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            }

            if (_configService.Config.RedactDebugPayloads)
            {
                json = Redact(json);
            }

            File.WriteAllText(path, json);
            return path;
        }
        catch
        {
            return "failed to save";
        }
    }

    private string Redact(string json)
    {
        string pattern = "\"(?:[a-zA-Z0-9]+Base64|audioBase64|screenshotBase64)\"\\s*:\\s*\"([^\"]+)\"";
        json = Regex.Replace(json, pattern, m => {
            var val = m.Groups[1].Value;
            if (val.Length > 100) {
                return m.Value.Replace(val, val.Substring(0, 10) + "...[REDACTED " + val.Length + " chars]..." + val.Substring(val.Length - 10));
            }
            return m.Value;
        });

        return json;
    }

    public string MaskSecret(string? secret)
    {
        if (string.IsNullOrEmpty(secret)) return "";
        if (secret.Length <= 8) return "****";
        return secret.Substring(0, 4) + "..." + secret.Substring(secret.Length - 4);
    }
}
