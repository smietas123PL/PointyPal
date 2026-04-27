using System.Text.RegularExpressions;

namespace PointyPal.AI;

public class PointTag
{
    public bool HasPoint { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? ScreenId { get; set; }
    public string CleanText { get; set; } = string.Empty;
}

public class PointTagParser
{
    private static readonly Regex PointTagRegex = new Regex(
        @"\[POINT:(?<content>[^\]]+)\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public PointTag Parse(string aiResponse)
    {
        var result = new PointTag { CleanText = aiResponse };
        var match = PointTagRegex.Match(aiResponse);

        if (!match.Success)
        {
            return result;
        }

        result.CleanText = PointTagRegex.Replace(aiResponse, "").Trim();
        string content = match.Groups["content"].Value;

        if (content.Equals("none", System.StringComparison.OrdinalIgnoreCase))
        {
            return result;
        }

        string[] parts = content.Split(':');
        if (parts.Length >= 2)
        {
            string[] coords = parts[0].Split(',');
            if (coords.Length == 2 && 
                double.TryParse(coords[0], out double x) && 
                double.TryParse(coords[1], out double y))
            {
                result.HasPoint = true;
                result.X = x;
                result.Y = y;
                result.Label = parts[1];

                if (parts.Length >= 3)
                {
                    result.ScreenId = parts[2];
                }
            }
        }

        return result;
    }
}
