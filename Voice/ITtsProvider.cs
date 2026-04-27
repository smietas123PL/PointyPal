using System.Threading;
using System.Threading.Tasks;

namespace PointyPal.Voice;

public interface ITtsProvider
{
    Task<TtsResult> GetSpeechAsync(TtsRequest request, CancellationToken token);
}
