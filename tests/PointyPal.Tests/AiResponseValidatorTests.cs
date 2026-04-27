using FluentAssertions;
using PointyPal.AI;
using Xunit;

namespace PointyPal.Tests;

public class AiResponseValidatorTests
{
    private readonly AiResponseValidator _validator = new();

    [Fact]
    public void Validate_ValidSinglePoint_ReturnsValid()
    {
        string input = "Click the button [POINT:100,100:button]";
        var result = _validator.Validate(input);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PointNone_ReturnsValid()
    {
        string input = "I don't see anything [POINT:none]";
        var result = _validator.Validate(input);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MultiplePoints_ReturnsInvalid()
    {
        string input = "First [POINT:1,1:a] second [POINT:2,2:b]";
        var result = _validator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.WarningMessage.Should().Contain("Multiple");
    }

    [Fact]
    public void Validate_PointNotAtEnd_ReturnsInvalid()
    {
        string input = "Here it is [POINT:10,10:here] and some more text.";
        var result = _validator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.WarningMessage.Should().Contain("not at the very end");
    }

    [Fact]
    public void Validate_Markdown_ReturnsInvalid()
    {
        string input = "Here is **bold** text [POINT:none]";
        var result = _validator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.WarningMessage.Should().Contain("markdown");
    }

    [Fact]
    public void Validate_Empty_ReturnsInvalid()
    {
        string input = "   ";
        var result = _validator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.WarningMessage.Should().Contain("empty");
    }

    [Fact]
    public void Validate_MissingPointTag_ReturnsInvalid()
    {
        string input = "This response has no point tag.";
        var result = _validator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.WarningMessage.Should().Contain("Missing");
    }
}
