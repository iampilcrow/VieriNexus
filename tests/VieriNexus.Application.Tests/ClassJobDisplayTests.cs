using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Application.Tests;

public sealed class ClassJobDisplayTests
{
    [Fact]
    public void UsesFullLocalizedNameAndUppercaseAbbreviation()
    {
        CharacterSnapshot character = new(
            new CharacterKey(1, 2), "Valentina Vieri", 31, 79, false, "Machinist", "mch");

        Assert.Equal("Machinist (MCH)", ClassJobDisplay.Label(character));
    }

    [Fact]
    public void UsesNonNumericFallbackWhenJobIsUnknown()
    {
        CharacterSnapshot character = new(new CharacterKey(1, 2), "Valentina Vieri", 31, 79, false);

        Assert.Equal("Unknown class/job", ClassJobDisplay.Label(character));
    }
}
