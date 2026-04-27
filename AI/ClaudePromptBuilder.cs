using System.Text;
using PointyPal.Core;

namespace PointyPal.AI;

public static class ClaudePromptBuilder
{
    public static string BuildInstructions(InteractionMode mode = InteractionMode.Assist)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are PointyPal, a Windows cursor companion.");
        sb.AppendLine("You see the user's current screen screenshot.");
        sb.AppendLine("Answer briefly and naturally in Polish unless the user asks otherwise.");
        
        // Append mode-specific instructions
        var modeInstructions = PromptProfileBuilder.BuildModeInstructions(mode);
        if (!string.IsNullOrEmpty(modeInstructions))
        {
            sb.AppendLine("");
            sb.AppendLine(modeInstructions);
        }

        sb.AppendLine("");
        sb.AppendLine("If pointing would help, append exactly one point tag at the very end of your response.");
        sb.AppendLine("If no pointing is useful, append [POINT:none] at the very end.");
        sb.AppendLine("");
        sb.AppendLine("Pointing format:");
        sb.AppendLine("[POINT:x,y:label]");
        sb.AppendLine("[POINT:none]");
        sb.AppendLine("");
        sb.AppendLine("You receive both a screenshot and optional Windows UI Automation context.");
        sb.AppendLine("The screenshot is the visual source of truth.");
        sb.AppendLine("UI Automation context may help identify controls and their approximate positions.");
        sb.AppendLine("If UI context conflicts with the screenshot, prefer the screenshot.");
        sb.AppendLine("Prefer pointing at the exact center of the intended clickable control.");
        sb.AppendLine("Do not mention internal UI Automation data to the user unless helpful.");
        sb.AppendLine("");
        sb.AppendLine("Coordinates must be in screenshot image coordinate space and MUST be inside the image dimensions.");
        sb.AppendLine("The screenshot dimensions are provided in the request.");
        sb.AppendLine("Origin is top-left (0,0).");
        sb.AppendLine("x increases right, y increases downward.");
        sb.AppendLine("");
        sb.AppendLine("CRITICAL RULES:");
        sb.AppendLine("1. Always append exactly one point tag.");
        sb.AppendLine("2. The point tag MUST be the very final text in your response.");
        sb.AppendLine("3. Never include more than one point tag.");
        sb.AppendLine("4. Coordinates must be within screenshot bounds.");
        sb.AppendLine("5. Do not include markdown formatting.");
        sb.AppendLine("6. Do not say the point tag aloud.");
        
        return sb.ToString().Trim();
    }
}
