using FluentAssertions;
using PointyPal.AI;
using Xunit;

namespace PointyPal.Tests;

public class PointTagParserTests
{
    private readonly PointTagParser _parser = new();

    [Fact]
    public void Parse_ValidSimpleTag_ReturnsPoint()
    {
        string input = "Click here [POINT:640,360:save button]";
        var result = _parser.Parse(input);

        result.HasPoint.Should().BeTrue();
        result.X.Should().Be(640);
        result.Y.Should().Be(360);
        result.Label.Should().Be("save button");
        result.CleanText.Should().Be("Click here");
    }

    [Fact]
    public void Parse_ValidTagWithScreen_ReturnsPointAndScreen()
    {
        string input = "Look at this [POINT:100,200:icon:screen1]";
        var result = _parser.Parse(input);

        result.HasPoint.Should().BeTrue();
        result.X.Should().Be(100);
        result.Y.Should().Be(200);
        result.Label.Should().Be("icon");
        result.ScreenId.Should().Be("screen1");
        result.CleanText.Should().Be("Look at this");
    }

    [Fact]
    public void Parse_PointNoneTag_ReturnsHasPointFalse()
    {
        string input = "I cannot find it [POINT:none]";
        var result = _parser.Parse(input);

        result.HasPoint.Should().BeFalse();
        result.CleanText.Should().Be("I cannot find it");
    }

    [Fact]
    public void Parse_NoTag_ReturnsHasPointFalseAndOriginalText()
    {
        string input = "Just some text.";
        var result = _parser.Parse(input);

        result.HasPoint.Should().BeFalse();
        result.CleanText.Should().Be("Just some text.");
    }

    [Fact]
    public void Parse_MalformedTag_ReturnsHasPointFalse()
    {
        string input = "Broken [POINT:abc,def:label]";
        var result = _parser.Parse(input);

        result.HasPoint.Should().BeFalse();
        result.CleanText.Should().Be("Broken");
    }

    [Fact]
    public void Parse_MultipleTags_UsesFirstTagAndCleansAll()
    {
        // Current implementation uses Regex.Match (first match) but Regex.Replace (all matches)
        string input = "First [POINT:1,1:one] second [POINT:2,2:two]";
        var result = _parser.Parse(input);

        result.HasPoint.Should().BeTrue();
        result.X.Should().Be(1);
        result.Y.Should().Be(1);
        result.Label.Should().Be("one");
        result.CleanText.Should().Be("First  second");
    }

    [Fact]
    public void Parse_TagInMiddle_CleansCorrectly()
    {
        string input = "Start [POINT:50,50:middle] end";
        var result = _parser.Parse(input);

        result.HasPoint.Should().BeTrue();
        result.CleanText.Should().Be("Start  end");
    }

    [Fact]
    public void Parse_LabelsWithSpaces_WorksCorrectly()
    {
        string input = "Check this [POINT:10,10:my very long label]";
        var result = _parser.Parse(input);

        result.HasPoint.Should().BeTrue();
        result.Label.Should().Be("my very long label");
    }
}
