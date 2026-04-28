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
        sb.AppendLine("If no pointing is useful, or if you are unsure, append [POINT:none] at the very end and explain briefly.");
        sb.AppendLine("");
        sb.AppendLine("Pointing format:");
        sb.AppendLine("[POINT:x,y:label]");
        sb.AppendLine("[POINT:none]");
        sb.AppendLine("");
        sb.AppendLine("GUIDELINES:");
        sb.AppendLine("- Point to the exact center of the intended clickable or visible target.");
        sb.AppendLine("- Avoid pointing to the edges or borders of controls.");
        sb.AppendLine("- Label should be very short: max 3-5 words (e.g., 'Przycisk Start', 'Pole wyszukiwania').");
        sb.AppendLine("- If UI Automation context is available, use it to confirm the center of elements.");
        sb.AppendLine("- Coordinates must be integers in screenshot image coordinate space (0 to Width-1, 0 to Height-1).");
        sb.AppendLine("");
        sb.AppendLine("CRITICAL RULES:");
        sb.AppendLine("1. Always append exactly one point tag.");
        sb.AppendLine("2. The point tag MUST be the very final text in your response.");
        sb.AppendLine("3. Never include more than one point tag.");
        sb.AppendLine("4. Coordinates must be within screenshot bounds.");
        sb.AppendLine("5. Do not include markdown formatting.");
        sb.AppendLine("6. Do not say the point tag aloud.");
        sb.AppendLine("7. Use [POINT:none] if you cannot find a clear target.");
        
        return sb.ToString().Trim();
    }
}
