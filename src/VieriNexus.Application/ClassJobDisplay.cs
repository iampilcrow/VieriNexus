using System.Globalization;
using VieriNexus.Domain;

namespace VieriNexus.Application;

public static class ClassJobDisplay
{
    public static string Label(CharacterSnapshot character)
    {
        ArgumentNullException.ThrowIfNull(character);
        return Label(character.ClassJobName, character.ClassJobAbbreviation);
    }

    public static string Label(string? classJobName, string? classJobAbbreviation)
    {
        string name = Name(classJobName);
        string abbreviation = Abbreviation(classJobAbbreviation);
        if (name.Length > 0 && abbreviation.Length > 0)
            return $"{name} ({abbreviation})";
        if (name.Length > 0)
            return name;
        if (abbreviation.Length > 0)
            return abbreviation;
        return "Unknown class/job";
    }

    public static string Name(string? value)
    {
        string name = value?.Trim() ?? string.Empty;
        return name.Length == 0
            ? string.Empty
            : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower(CultureInfo.CurrentCulture));
    }

    public static string Abbreviation(string? value) =>
        (value?.Trim() ?? string.Empty).ToUpperInvariant();
}
