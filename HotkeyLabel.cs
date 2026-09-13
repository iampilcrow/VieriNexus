namespace VieriRotationHelper;

internal static class HotkeyLabel
{
    internal static string Normalize(string label) => label.Length == 0 ? label : label[0] switch
    {
        '§' => "S" + label[1..], 'ª' => "A" + label[1..], '¢' => "C" + label[1..],
        '¾' => "CA" + label[1..], '½' => "CS" + label[1..], '¼' => "AS" + label[1..],
        '¶' => "CAS" + label[1..], _ => label,
    };
}
