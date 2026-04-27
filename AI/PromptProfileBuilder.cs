using System.Text;
using PointyPal.Core;

namespace PointyPal.AI;

public static class PromptProfileBuilder
{
    public static string BuildModeInstructions(InteractionMode mode)
    {
        var sb = new StringBuilder();
        
        switch (mode)
        {
            case InteractionMode.Assist:
                sb.AppendLine("Mode: Assist. Provide a balanced answer. Point only if it helps the user find something or understand the context.");
                break;
            case InteractionMode.Point:
                sb.AppendLine("Mode: Point. Prioritize identifying what to click or look at. A point tag is REQUIRED unless absolutely no relevant element is visible.");
                sb.AppendLine("- Point at the exact center of the intended clickable control.");
                sb.AppendLine("- Be extremely concise.");
                sb.AppendLine("- Prefer actionable guidance.");
                break;
            case InteractionMode.Explain:
                sb.AppendLine("Mode: Explain. Explain what is visible on the screen or what a specific element does. Point only if it helps clarify your explanation.");
                break;
            case InteractionMode.Summarize:
                sb.AppendLine("Mode: Summarize. Briefly summarize the visible content. Usually, no pointing is needed.");
                sb.AppendLine("Unless specifically asked to point, use [POINT:none].");
                break;
            case InteractionMode.ReadScreen:
                sb.AppendLine("Mode: ReadScreen. Extract and read visible text from the screenshot (OCR-like).");
                sb.AppendLine("- Do not hallucinate text that is not clearly visible.");
                sb.AppendLine("- Prioritize accuracy over completeness.");
                sb.AppendLine("Usually, no pointing is needed. Use [POINT:none].");
                break;
            case InteractionMode.Debug:
                sb.AppendLine("Mode: Debug. Help debug visible errors, code issues, or UI glitches.");
                sb.AppendLine("- Be precise and technical.");
                sb.AppendLine("- Cite visible error messages or UI labels from the screenshot/UI context.");
                sb.AppendLine("Point if it helps identify the source of the problem.");
                break;
            case InteractionMode.Translate:
                sb.AppendLine("Mode: Translate. Translate visible text or user-provided text.");
                sb.AppendLine("- Preserve the original meaning and tone.");
                sb.AppendLine("- Do not point unless explicitly asked.");
                sb.AppendLine("Always use [POINT:none] unless a point is requested.");
                break;
            case InteractionMode.NoPoint:
                sb.AppendLine("Mode: NoPoint. NEVER include a point coordinate. ALWAYS append [POINT:none] at the end of your response.");
                break;
        }

        return sb.ToString().Trim();
    }
}
