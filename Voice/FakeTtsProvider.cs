using System.Threading;
using System.Threading.Tasks;

namespace PointyPal.Voice;

public class FakeTtsProvider : ITtsProvider
{
    public async Task<TtsResult> GetSpeechAsync(TtsRequest request, CancellationToken token)
    {
        // Simulate some network delay
        await Task.Delay(500, token);

        return new TtsResult
        {
            Success = true,
            ProviderName = "Fake",
            AudioPath = "", // No audio file produced
            DurationMs = request.Text.Length * 50 // Fake duration
        };
    }
}
