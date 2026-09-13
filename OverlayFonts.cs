using Dalamud.Interface.ManagedFontAtlas;

namespace VieriRotationHelper;

internal sealed class OverlayFonts : IDisposable
{
    internal IFontHandle Miedinger { get; }

    internal OverlayFonts()
    {
        using var stream = typeof(OverlayFonts).Assembly.GetManifestResourceStream("VieriRotationHelper.Media.Fonts.Miedinger.ttf")
            ?? throw new InvalidOperationException("The packaged Hilda display font is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var fontBytes = buffer.ToArray();
        Miedinger = Plugin.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(toolkit =>
            toolkit.OnPreBuild(build => build.AddFontFromMemory(fontBytes, new SafeFontConfig { SizePx = 26f }, "Hilda Miedinger")));
    }

    public void Dispose() => Miedinger.Dispose();
}
