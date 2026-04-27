using System;

namespace PointyPal.Voice;

public class TtsRequest
{
    public string Text { get; set; } = "";
    public string VoiceId { get; set; } = "";
    public string ModelId { get; set; } = "eleven_flash_v2_5";
    public string OutputFormat { get; set; } = "mp3_44100_128";
}

public class TtsResult
{
    public bool Success { get; set; }
    public string AudioPath { get; set; } = "";
    public string AudioMimeType { get; set; } = "audio/mpeg";
    public string ProviderName { get; set; } = "";
    public double DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RequestId { get; set; }
}
