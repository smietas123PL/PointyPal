using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PointyPal.AI;

public class FakeAiResponseProvider : IAiResponseProvider
{
    public async Task<AiResponse> GetResponseAsync(AiRequest request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        
        // Simulate network delay
        await Task.Delay(1000, cancellationToken);

        string lowerInput = request.UserText.ToLowerInvariant();
        string responseText;

        if (lowerInput.Contains("center"))
        {
            int cx = request.ScreenshotWidth / 2;
            int cy = request.ScreenshotHeight / 2;
            responseText = $"Jasne, wskazuję środek ekranu. [POINT:{cx},{cy}:center]";
        }
        else if (lowerInput.Contains("top right"))
        {
            int tx = (int)(request.ScreenshotWidth * 0.85);
            int ty = (int)(request.ScreenshotHeight * 0.15);
            responseText = $"Wskazuję w prawy górny róg. [POINT:{tx},{ty}:top right]";
        }
        else if (lowerInput.Contains("none"))
        {
            responseText = "Nie muszę nic wskazywać. [POINT:none]";
        }
        else
        {
            int dx = (int)(request.ScreenshotWidth * 0.70);
            int dy = (int)(request.ScreenshotHeight * 0.35);
            responseText = $"Oto domyślny punkt. [POINT:{dx},{dy}:default point]";
        }

        sw.Stop();
        return new AiResponse
        {
            RawText = responseText,
            ProviderName = "Fake",
            DurationMs = sw.ElapsedMilliseconds
        };
    }
}
