using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PointyPal.Infrastructure;
using System.Diagnostics;
using System.Linq;

namespace PointyPal.Voice;

public class WorkerTtsProvider : ITtsProvider
{
    private readonly HttpClient _httpClient;
    private readonly ConfigService _configService;

    public WorkerTtsProvider(ConfigService configService)
    {
        _httpClient = new HttpClient();
        _configService = configService;
    }

    public async Task<TtsResult> GetSpeechAsync(TtsRequest request, CancellationToken token)
    {
        var config = _configService.Config;
        var result = new TtsResult { ProviderName = "Worker" };

        if (string.IsNullOrEmpty(config.WorkerClientKey))
        {
            result.ErrorMessage = "Worker client key is missing.";
            return result;
        }

        if (string.IsNullOrEmpty(config.WorkerBaseUrl))
        {
            result.ErrorMessage = "WorkerBaseUrl is not configured.";
            return result;
        }

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string debugDir = Path.Combine(appData, "PointyPal", "debug");
        Directory.CreateDirectory(debugDir);

        bool saveArtifact = config.SaveDebugArtifacts && config.SaveTtsAudio;
        string audioFileName = saveArtifact ? "latest-tts.mp3" : $"temp-tts-{Guid.NewGuid()}.mp3";
        string audioPath = Path.Combine(debugDir, audioFileName);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            if (config.TtsRequestTimeoutSeconds > 0)
            {
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(config.TtsRequestTimeoutSeconds));
            }

            var requestToken = timeoutCts.Token;

            // 2. Call Worker
            string url = $"{config.WorkerBaseUrl.TrimEnd('/')}/tts";
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Content = JsonContent.Create(request);
            httpRequest.Headers.Add("X-PointyPal-Client-Key", config.WorkerClientKey);

            var response = await _httpClient.SendAsync(httpRequest, requestToken);

            if (response.Headers.TryGetValues("X-Request-Id", out var values))
            {
                result.RequestId = values.FirstOrDefault();
                Debug.WriteLine($"TTS Request ID: {result.RequestId}");
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(requestToken);
                string? requestId = null;
                try {
                    var errDoc = JsonDocument.Parse(errorBody);
                    requestId = errDoc.RootElement.TryGetProperty("requestId", out var rid) ? rid.GetString() : null;
                    if (errDoc.RootElement.TryGetProperty("message", out var msg)) errorBody = msg.GetString();
                    else if (errDoc.RootElement.TryGetProperty("error", out var err)) errorBody = err.GetString();
                } catch { }

                result.ErrorMessage = $"Worker returned {response.StatusCode}: {errorBody} (RID: {requestId})";
                return result;
            }

            var workerResult = await response.Content.ReadFromJsonAsync<WorkerTtsResponse>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, requestToken);

            if (workerResult == null || string.IsNullOrEmpty(workerResult.AudioBase64))
            {
                result.ErrorMessage = "Worker returned empty audio data.";
                return result;
            }

            // 3. Save audio file
            byte[] audioBytes = Convert.FromBase64String(workerResult.AudioBase64);
            await File.WriteAllBytesAsync(audioPath, audioBytes, requestToken);

            result.Success = true;
            result.AudioPath = audioPath;
            result.AudioMimeType = workerResult.AudioMimeType;
            result.DurationMs = workerResult.DurationMs;
            
            return result;
        }
        catch (OperationCanceledException)
        {
            result.ErrorMessage = "TTS request timed out or was cancelled.";
            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Exception in WorkerTtsProvider: {ex.Message}";
            return result;
        }
    }

    private class WorkerTtsResponse
    {
        public string AudioBase64 { get; set; } = "";
        public string AudioMimeType { get; set; } = "";
        public string Provider { get; set; } = "";
        public double DurationMs { get; set; }
    }
}
