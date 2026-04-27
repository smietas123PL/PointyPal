using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PointyPal.Infrastructure;

namespace PointyPal.AI;

public class ClaudeVisionResponseProvider : IAiResponseProvider
{
    private readonly HttpClient _httpClient;
    private readonly AppConfig _config;

    public ClaudeVisionResponseProvider(AppConfig config)
    {
        _config = config;
        _httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<AiResponse> GetResponseAsync(AiRequest request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var result = new AiResponse { ProviderName = "Claude" };

        if (string.IsNullOrEmpty(_config.WorkerClientKey))
        {
            result.ErrorMessage = "Worker client key is missing.";
            return result;
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            int timeoutSeconds = _config.ClaudeRequestTimeoutSeconds > 0
                ? _config.ClaudeRequestTimeoutSeconds
                : _config.RequestTimeoutSeconds;
            if (timeoutSeconds > 0)
            {
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            }

            var token = timeoutCts.Token;

            var workerRequest = new
            {
                model = string.IsNullOrEmpty(request.ModelOverride) ? _config.ClaudeModel : request.ModelOverride,
                userText = request.UserText,
                screenshotBase64 = request.ScreenshotBase64,
                screenshotMimeType = request.ScreenshotMimeType,
                screenshotWidth = request.ScreenshotWidth,
                screenshotHeight = request.ScreenshotHeight,
                cursorImagePosition = new { x = request.CursorImagePosition.X, y = request.CursorImagePosition.Y },
                monitorBounds = new 
                { 
                    left = request.MonitorBounds.Left, 
                    top = request.MonitorBounds.Top, 
                    width = request.MonitorBounds.Width, 
                    height = request.MonitorBounds.Height 
                },
                instructions = request.PromptInstructions,
                interactionMode = _config.DefaultInteractionMode.ToString(),
                promptProfileInstructions = "" // Could be added to IAiRequest if needed
            };

            string url = _config.WorkerBaseUrl.TrimEnd('/') + "/chat";
            
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Content = JsonContent.Create(workerRequest);
            httpRequest.Headers.Add("X-PointyPal-Client-Key", _config.WorkerClientKey);

            var response = await _httpClient.SendAsync(httpRequest, token);
            
            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync(token);
                try {
                    var errDoc = JsonDocument.Parse(errorContent);
                    string? requestId = errDoc.RootElement.TryGetProperty("requestId", out var rid) ? rid.GetString() : null;
                    if (errDoc.RootElement.TryGetProperty("message", out var msgProp)) {
                        result.ErrorMessage = $"Worker Error: {msgProp.GetString()} (RID: {requestId})";
                    } else if (errDoc.RootElement.TryGetProperty("error", out var errProp)) {
                        result.ErrorMessage = $"Worker Error: {errProp.GetString()} (RID: {requestId})";
                    } else {
                        result.ErrorMessage = $"Worker error ({response.StatusCode}): {errorContent} (RID: {requestId})";
                    }
                } catch {
                    result.ErrorMessage = $"Worker error ({response.StatusCode}): {errorContent}";
                }
                return result;
            }

            var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: token);
            
            if (jsonResponse.TryGetProperty("requestId", out var ridProp))
            {
                string? rid = ridProp.GetString();
                result.RequestId = rid;
                Debug.WriteLine($"Worker Request ID: {rid}");
            }

            if (jsonResponse.TryGetProperty("text", out var textProp))
            {
                result.RawText = textProp.GetString() ?? "";
            }
            else if (jsonResponse.TryGetProperty("content", out var contentProp))
            {
                result.RawText = contentProp.GetString() ?? "";
            }
            else
            {
                result.ErrorMessage = "Unexpected response format from worker (missing 'text' or 'content' property).";
            }
        }
        catch (OperationCanceledException)
        {
            result.ErrorMessage = "Request timed out or was cancelled.";
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Exception: {ex.Message}";
        }
        finally
        {
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
        }

        return result;
    }

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            string url = _config.WorkerBaseUrl.TrimEnd('/') + "/health";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return false;
            
            var data = await response.Content.ReadFromJsonAsync<JsonElement>();
            return data.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
        }
        catch
        {
            return false;
        }
    }
}
