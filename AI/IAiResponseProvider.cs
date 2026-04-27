using System.Threading;
using System.Threading.Tasks;

namespace PointyPal.AI;

public interface IAiResponseProvider
{
    Task<AiResponse> GetResponseAsync(AiRequest request, CancellationToken cancellationToken);
}
