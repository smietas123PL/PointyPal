using System.Threading;
using System.Threading.Tasks;

namespace PointyPal.Voice;

public class TranscriptRequest
{
    public string AudioFilePath { get; set; } = "";
}

public class TranscriptResult
{
    public string Text { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public string AudioFilePath { get; set; } = "";
    public double DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RequestId { get; set; }
}

public interface ITranscriptProvider
{
    Task<TranscriptResult> GetTranscriptAsync(TranscriptRequest request, CancellationToken ct);
}
