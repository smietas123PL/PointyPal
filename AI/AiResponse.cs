namespace PointyPal.AI;

public class AiResponse
{
    public string RawText { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public long DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RequestId { get; set; }
}
