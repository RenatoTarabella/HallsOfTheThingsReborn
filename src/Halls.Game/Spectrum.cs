using System.Numerics;
using Halls.Core;
using Raylib_cs;

namespace Halls.Game;

/// <summary>
/// The screen of the Spectrum: 32x24 cells of 8x8 pixels, in the middle of a
/// 40x32 canvas (320x256, as on the Archimedes) whose border holds the
/// statistics. Cells are addressed in Spectrum coordinates: (0,0) is the top
/// left cell of the 32x24, the border is at negative columns or rows 24-27.
/// </summary>
public sealed class Spectrum : IDisposable
{
    public const int Width = 320, Height = 256, Left = 32, Top = 32;

    public Texture2D Atlas { get; }
    public RenderTexture2D Canvas { get; }

    public Spectrum()
    {
        // the 256 glyphs, white on transparent, 16x16 of 8x8
        var img = Raylib.GenImageColor(128, 128, new Color(0, 0, 0, 0));
        for (int c = 0; c < 256; c++)
            for (int y = 0; y < 8; y++)
            {
                int bits = CharSet.Row(c, y);
                for (int x = 0; x < 8; x++)
                    if ((bits & (0x80 >> x)) != 0)
                        Raylib.ImageDrawPixel(ref img, (c & 15) * 8 + x, (c >> 4) * 8 + y, Color.White);
            }
        Atlas = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        Canvas = Raylib.LoadRenderTexture(Width, Height);
        Raylib.SetTextureFilter(Canvas.Texture, TextureFilter.Point);
    }

    public static Rectangle GlyphRect(int code) => new((code & 15) * 8, ((code >> 4) & 15) * 8, 8, 8);

    public static Color Ink(int code)
    {
        var (r, g, b) = CharSet.Ink(code);
        return new Color(r, g, b, (byte)255);
    }

    /// <summary>OutIt: a code at a cell, in the colour of its attribute.</summary>
    public void Cell(int col, int row, int code) =>
        Raylib.DrawTextureRec(Atlas, GlyphRect(code), new Vector2(Left + col * 8, Top + row * 8), Ink(code));

    public void Cell(int col, int row, int code, Color ink) =>
        Raylib.DrawTextureRec(Atlas, GlyphRect(code), new Vector2(Left + col * 8, Top + row * 8), ink);

    /// <summary>The colour of the strong Things (not in 1983): bright red.</summary>
    public static readonly Color Strong = new(255, 0, 0, 255);

    public void Text(int col, int row, string text)
    {
        for (int i = 0; i < text.Length; i++) Cell(col + i, row, text[i] & 0x7F);
    }

    public void Text(int col, int row, string text, Color ink)
    {
        for (int i = 0; i < text.Length; i++) Cell(col + i, row, text[i] & 0x7F, ink);
    }

    /// <summary>The FLASH of the Spectrum: on and off every 0.32 s.</summary>
    public bool Blink => Raylib.GetTime() % 0.64 < 0.32;

    public void Begin()
    {
        Raylib.BeginTextureMode(Canvas);
        Raylib.ClearBackground(Color.Black);
    }

    public void End() => Raylib.EndTextureMode();

    /// <summary>
    /// The canvas on the window, as large as it fits keeping the 5:4 shape
    /// (part = the rows of pixels to show, from <paramref name="fromY"/>).
    /// </summary>
    public void Present(Rectangle dest, int fromY = 0, int rows = Height)
    {
        var src = new Rectangle(0, Height - fromY - rows, Width, -rows);
        Raylib.DrawTexturePro(Canvas.Texture, src, dest, Vector2.Zero, 0, Color.White);
    }

    public static Rectangle Fit(int w, int h, float aspectW, float aspectH)
    {
        float s = MathF.Min(w / aspectW, h / aspectH);
        // whole multiples of the pixels when they are large enough, for sharp edges
        if (s >= 2) s = MathF.Floor(s);
        float dw = aspectW * s, dh = aspectH * s;
        return new Rectangle((w - dw) / 2, (h - dh) / 2, dw, dh);
    }

    public void Dispose()
    {
        Raylib.UnloadTexture(Atlas);
        Raylib.UnloadRenderTexture(Canvas);
    }
}
