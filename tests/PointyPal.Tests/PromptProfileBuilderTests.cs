using FluentAssertions;
using PointyPal.AI;
using PointyPal.Core;
using Xunit;

namespace PointyPal.Tests;

public class PromptProfileBuilderTests
{
    [Fact]
    public void BuildModeInstructions_Assist_IncludesBalancedInstructions()
    {
        var instructions = PromptProfileBuilder.BuildModeInstructions(InteractionMode.Assist);
        instructions.Should().Contain("balanced answer");
    }

    [Fact]
    public void BuildModeInstructions_Point_PrioritizesActionablePointing()
    {
        var instructions = PromptProfileBuilder.BuildModeInstructions(InteractionMode.Point);
        instructions.Should().Contain("Prioritize identifying");
        instructions.Should().Contain("actionable guidance");
    }

    [Fact]
    public void BuildModeInstructions_NoPoint_ForcesNoneTag()
    {
        var instructions = PromptProfileBuilder.BuildModeInstructions(InteractionMode.NoPoint);
        instructions.Should().Contain("ALWAYS append [POINT:none]");
    }

    [Fact]
    public void BuildModeInstructions_ReadScreen_DiscouragesHallucination()
    {
        var instructions = PromptProfileBuilder.BuildModeInstructions(InteractionMode.ReadScreen);
        instructions.Should().Contain("Do not hallucinate");
    }

    [Fact]
    public void BuildModeInstructions_Debug_AsksForPreciseEvidence()
    {
        var instructions = PromptProfileBuilder.BuildModeInstructions(InteractionMode.Debug);
        instructions.Should().Contain("Cite visible error messages");
        instructions.Should().Contain("technical");
    }

    [Fact]
    public void BuildModeInstructions_Translate_UsuallyAvoidsPointing()
    {
        var instructions = PromptProfileBuilder.BuildModeInstructions(InteractionMode.Translate);
        instructions.Should().Contain("Always use [POINT:none]");
    }
}
