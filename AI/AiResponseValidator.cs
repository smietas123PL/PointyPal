using System;
using System.Text.RegularExpressions;

namespace PointyPal.AI;

public class AiResponseValidationResult
{
    public bool IsValid { get; set; }
    public string? WarningMessage { get; set; }
}

public class AiResponseValidator
{
    public AiResponseValidationResult Validate(string rawText)
    {
        var result = new AiResponseValidationResult { IsValid = true };

        if (string.IsNullOrWhiteSpace(rawText))
        {
            result.IsValid = false;
            result.WarningMessage = "Response is empty.";
            return result;
        }

        // 1. Check for multiple [POINT] tags
        var matches = Regex.Matches(rawText, @"\[POINT:", RegexOptions.IgnoreCase);
        if (matches.Count == 0)
        {
            result.IsValid = false;
            result.WarningMessage = "Missing [POINT] tag.";
            return result;
        }

        if (matches.Count > 1)
        {
            result.IsValid = false;
            result.WarningMessage = "Multiple [POINT] tags detected.";
            return result;
        }

        // 2. Check if point tag is at the end (if present)
        if (rawText.Contains("[POINT:", StringComparison.OrdinalIgnoreCase))
        {
            int tagStart = rawText.LastIndexOf("[POINT:", StringComparison.OrdinalIgnoreCase);
            string afterTag = rawText.Substring(tagStart);
            int tagEnd = afterTag.IndexOf("]");
            
            if (tagEnd != -1)
            {
                string remainder = afterTag.Substring(tagEnd + 1).Trim();
                if (!string.IsNullOrEmpty(remainder))
                {
                    result.IsValid = false;
                    result.WarningMessage = "Point tag is not at the very end of the response.";
                }
            }
        }

        // 3. Check for markdown
        if (rawText.Contains("```") || rawText.Contains("**") || rawText.Contains("###"))
        {
            result.IsValid = false;
            result.WarningMessage = "Response contains markdown formatting.";
        }

        return result;
    }
}
