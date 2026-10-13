using System.Numerics;
using Halls.Core;
using Raylib_cs;

namespace Halls.Game;

/// <summary>
/// The map of the floor (P, not in 1983): the whole BigM drawn with the characters,
/// but only the cells already seen on the screen, without the Things and the
/// projectiles. The game stands still while it is shown.
/// </summary>
public sealed class MapView : IDisposable
{
    const int W = 128 * 8, H = 113 * 8 + 24;

    readonly Spectrum spectrum;
    readonly RenderTexture2D canvas;
    readonly bool[][] seen = Enumerable.Range(0, 8).Select(_ => new bool[128 * 128]).ToArray();
    // the sanctuary is the same on every floor
    readonly bool[] seenSanctuary = new bool[128 * 128];
    float time;

    public MapView(Spectrum spectrum)
    {
        this.spectrum = spectrum;
        canvas = Raylib.LoadRenderTexture(W, H);
        Raylib.SetTextureFilter(canvas.Texture, TextureFilter.Point);
    }

    public void Reset()
    {
        foreach (var f in seen) Array.Clear(f);
        Array.Clear(seenSanctuary);
    }

    /// <summary>The cells x0..x1, y0..y1 have been on the screen.</summary>
    public void MarkSeen(Core.Game g, int x0, int y0, int x1, int y1)
    {
        var floor = seen[g.Floor];
        for (int row = Math.Max(0, y0); row <= Math.Min(112, y1); row++)
            for (int col = Math.Max(0, x0); col <= Math.Min(127, x1); col++)
                (col > 112 ? seenSanctuary : floor)[row * 128 + col] = true;
    }

    bool Seen(Core.Game g, int p) => (p & 127) > 112 ? seenSanctuary[p] : seen[g.Floor][p];

    void Glyph(int col, int row, int code, Color ink) =>
        Raylib.DrawTextureRec(spectrum.Atlas, Spectrum.GlyphRect(code), new Vector2(col * 8, row * 8), ink);

    public void Draw(Core.Game g, float dt)
    {
        time += dt;
        Raylib.BeginTextureMode(canvas);
        Raylib.ClearBackground(Color.Black);
        for (int row = 0; row < 113; row++)
            for (int col = 0; col < 128; col++)
            {
                int p = row * 128 + col;
                if (!Seen(g, p)) continue;
                int c = g.Shown(col, row);
                // what moves is not on the map (the disguised Thing looks like treasure, as on the screen)
                if (c == Codes.Space || Codes.IsMan(c) || Codes.IsProjectile(c) || c >= Codes.Sword && c < Codes.BarEmpty
                    || Codes.IsThing(c) && c != Codes.Disguised) continue;
                Glyph(col, row, c, Spectrum.Ink(c));
            }
        if (time % 0.6f < 0.4f)
        {
            int m = Codes.Man + 2 * g.ManDir;
            Glyph(g.ManPos & 127, g.ManPos >> 7, m, Color.White);
            Glyph(g.ManPos & 127, (g.ManPos >> 7) + 1, m + 1, Color.White);
        }
        string title = (g.HasKey ? "Floor 0 - you have the golden key" : $"Floor {g.Floor}") + "      P: back to the game";
        for (int i = 0; i < title.Length; i++) Glyph(i + 1, 114, title[i], Color.White);
        Raylib.EndTextureMode();

        Raylib.ClearBackground(Color.Black);
        var dest = Spectrum.Fit(Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), W, H);
        Raylib.DrawTexturePro(canvas.Texture, new Rectangle(0, 0, W, -H), dest, Vector2.Zero, 0, Color.White);
    }

    public void Save(BinaryWriter w)
    {
        foreach (var f in seen.Append(seenSanctuary))
            foreach (bool b in f) w.Write(b);
    }

    public void Load(BinaryReader r)
    {
        foreach (var f in seen.Append(seenSanctuary))
            for (int i = 0; i < f.Length; i++) f[i] = r.ReadBoolean();
    }

    public void Dispose() => Raylib.UnloadRenderTexture(canvas);
}
