using VieriNexus.Domain;

namespace VieriNexus.Application;

public static class ClassJobDisplay
{
    public static string Label(CharacterSnapshot character)
    {
        ArgumentNullException.ThrowIfNull(character);
        string name = character.ClassJobName.Trim();
        string abbreviation = character.ClassJobAbbreviation.Trim().ToUpperInvariant();
        if (name.Length > 0 && abbreviation.Length > 0)
            return $"{name} ({abbreviation})";
        if (name.Length > 0)
            return name;
        if (abbreviation.Length > 0)
            return abbreviation;
        return "Unknown class/job";
    }
}
