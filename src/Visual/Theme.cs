using Raylib_cs;

namespace WaveFunctionCollapse.Visual;

internal static class Theme
{
    internal static readonly Color Background = new(10, 17, 25);
    internal static readonly Color Panel = new(18, 29, 40);
    internal static readonly Color Border = new(35, 52, 66);
    internal static readonly Color Text = new(222, 234, 238);
    internal static readonly Color Muted = new(137, 160, 175);
    internal static readonly Color Cyan = new(98, 226, 210);
    internal static readonly Color Gold = new(242, 198, 103);
    internal static readonly Color Red = new(244, 113, 135);

    internal static void TextAt(string text, int x, int y, int size = 18, Color? color = null) =>
        Raylib.DrawText(text, x, y, size, color ?? Text);

    internal static void Card(Rectangle rect)
    {
        Raylib.DrawRectangleRounded(rect, 0.035f, 6, Panel);
        Raylib.DrawRectangleRoundedLinesEx(rect, 0.035f, 6, 1, Border);
    }
}
