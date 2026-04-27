using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PointyPal.Infrastructure;
using System.Diagnostics;
using System.Linq;

namespace PointyPal.Voice;

public class WorkerTranscriptProvider : ITranscriptProvider
{
    private readonly AppConfig _config;
    private readonly HttpClient _httpClient;

    public WorkerTranscriptProvider(AppConfig config)
    {
        _config = config;
        _httpClient = new HttpClient();
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task<TranscriptResult> GetTranscriptAsync(TranscriptRequest request, CancellationToken ct)
    {
        var result = new TranscriptResult { ProviderName = "Worker", AudioFilePath = request.AudioFilePath };
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        int timeoutSeconds = _config.TranscriptRequestTimeoutSeconds > 0
            ? _config.TranscriptRequestTimeoutSeconds
            : _config.RequestTimeoutSeconds;
        if (timeoutSeconds > 0)
        {
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        }

        var token = timeoutCts.Token;

        try
        {
            if (string.IsNullOrEmpty(_config.WorkerClientKey))
            {
                return new TranscriptResult
                {
                    ProviderName = "Worker",
                    AudioFilePath = request.AudioFilePath,
                    ErrorMessage = "WorkerClientKey is missing. Open Control Center > Connection and enter your Worker client key."
                };
            }

            if (string.IsNullOrEmpty(_config.WorkerBaseUrl) || _config.WorkerBaseUrl.Contains("YOUR-WORKER"))
            {
                return new TranscriptResult
                {
                    ProviderName = "Worker",
                    AudioFilePath = request.AudioFilePath,
                    ErrorMessage = "WorkerBaseUrl is not configured. Open Control Center > Connection and enter your Worker URL."
                };
            }

            if (!File.Exists(request.AudioFilePath))
            {
                return new TranscriptResult { ErrorMessage = "Nie znalazłem nagrania." };
            }

            var fileInfo = new FileInfo(request.AudioFilePath);
            if (fileInfo.Length > _config.MaxAudioUploadBytes)
            {
                return new TranscriptResult { ErrorMessage = "Nagranie jest za długie." };
            }

            byte[] audioBytes = await File.ReadAllBytesAsync(request.AudioFilePath, token);
            string audioBase64 = Convert.ToBase64String(audioBytes);

            var workerRequest = new
            {
                audioBase64 = audioBase64,
                audioMimeType = "audio/wav",
                language = _config.TranscriptionLanguage,
                filename = Path.GetFileName(request.AudioFilePath)
            };

            string jsonRequest = JsonSerializer.Serialize(workerRequest);
            
            string url = _config.WorkerBaseUrl.TrimEnd('/') + "/transcribe";
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
            httpRequest.Headers.Add("X-PointyPal-Client-Key", _config.WorkerClientKey);

            var response = await _httpClient.SendAsync(httpRequest, token);
            string jsonResponse = await response.Content.ReadAsStringAsync(token);

            if (response.Headers.TryGetValues("X-Request-Id", out var values))
            {
                result.RequestId = values.FirstOrDefault();
                Debug.WriteLine($"Transcript Request ID: {result.RequestId}");
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorDoc = JsonDocument.Parse(jsonResponse);
                string? requestId = errorDoc.RootElement.TryGetProperty("requestId", out var rid) ? rid.GetString() : null;
                string msg = errorDoc.RootElement.TryGetProperty("message", out var m) ? m.GetString() ?? "Unknown" : 
                            errorDoc.RootElement.TryGetProperty("error", out var err) ? err.GetString() ?? "Unknown error" : "HTTP " + response.StatusCode;
                return new TranscriptResult { ErrorMessage = $"Błąd Workera: {msg} (RID: {requestId})" };
            }

            var resultDoc = JsonDocument.Parse(jsonResponse);
            if (resultDoc.RootElement.TryGetProperty("requestId", out var ridProp))
            {
                System.Diagnostics.Debug.WriteLine($"Worker Request ID: {ridProp.GetString()}");
            }

            return new TranscriptResult
            {
                Text = resultDoc.RootElement.GetProperty("text").GetString() ?? "",
                ProviderName = resultDoc.RootElement.GetProperty("provider").GetString() ?? "worker",
                AudioFilePath = request.AudioFilePath,
                DurationMs = resultDoc.RootElement.GetProperty("durationMs").GetDouble()
            };
        }
        catch (OperationCanceledException)
        {
            return new TranscriptResult { ErrorMessage = "Anulowano." };
        }
        catch (Exception ex)
        {
            return new TranscriptResult { ErrorMessage = $"Błąd: {ex.Message}" };
        }
    }
}
