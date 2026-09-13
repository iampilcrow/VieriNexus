using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Application.Tests;

public sealed class ClassJobDisplayTests
{
    [Fact]
    public void UsesFullLocalizedNameAndUppercaseAbbreviation()
    {
        CharacterSnapshot character = new(
            new CharacterKey(1, 2), "Valentina Vieri", 35, 79, false, "red mage", "rdm");

        Assert.Equal("Red Mage (RDM)", ClassJobDisplay.Label(character));
    }

    [Theory]
    [InlineData("black mage", "Black Mage")]
    [InlineData("WARRIOR", "Warrior")]
    [InlineData("  machinist  ", "Machinist")]
    public void UsesPlayerFacingTitleCaseForEveryJobName(string source, string expected)
    {
        Assert.Equal(expected, ClassJobDisplay.Name(source));
    }

    [Fact]
    public void UsesNonNumericFallbackWhenJobIsUnknown()
    {
        CharacterSnapshot character = new(new CharacterKey(1, 2), "Valentina Vieri", 31, 79, false);

        Assert.Equal("Unknown class/job", ClassJobDisplay.Label(character));
    }
}
