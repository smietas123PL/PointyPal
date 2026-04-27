using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PointyPal.Voice;

public class FakeTranscriptProvider : ITranscriptProvider
{
    private readonly string _fakeText;

    public FakeTranscriptProvider(string fakeText = "Co powinienem kliknąć na tym ekranie?")
    {
        _fakeText = fakeText;
    }

    public async Task<TranscriptResult> GetTranscriptAsync(TranscriptRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        
        // Simulate some work
        await Task.Delay(500, ct);

        sw.Stop();
        return new TranscriptResult
        {
            Text = _fakeText,
            ProviderName = "Fake",
            AudioFilePath = request.AudioFilePath,
            DurationMs = sw.ElapsedMilliseconds
        };
    }
}
