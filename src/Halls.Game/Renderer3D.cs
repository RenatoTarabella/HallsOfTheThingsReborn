using System.Numerics;
using Halls.Core;
using Raylib_cs;

namespace Halls.Game;

/// <summary>
/// The map seen from above in 3D: the same cells as the classic screen, one cell
/// is one unit (x = column, z = row, north = -z). Every code becomes a model of
/// Assets/Models when there is one, else a box or a card with its character.
/// What is outside the 32x24 screen of the game is drawn dark and without the
/// Things, so that the 3D view shows what the classic one shows.
/// </summary>
public sealed class Renderer3D : IDisposable
{
    static readonly (float Pitch, float Distance)[] Presets = [(89f, 35f), (62f, 31f), (42f, 27f)];

    readonly Spectrum spectrum;
    readonly Shader lit, cutout;
    readonly int litLightDir;
    readonly ModelLibrary models;
    Camera3D camera;
    Vector2 target;
    bool snapped;
    float zoom = 1;
    float time;
    Vector3 cardUp = Vector3.UnitY, cardRight = Vector3.UnitX;

    readonly Dictionary<int, Thing> thingAt = new();
    readonly Dictionary<int, Projectile> projectileAt = new();

    public int Preset { get; set; }
    public bool StrongInRed { get; set; } = true;

    /// <summary>The colour of the man (white, or the warning near death).</summary>
    public Color ManColour { get; set; } = Color.White;
    public IReadOnlyList<string> ModelErrors => models.Errors;
    public int ModelCount => models.Keys.Count;

    public Renderer3D(Spectrum spectrum)
    {
        this.spectrum = spectrum;
        lit = Raylib.LoadShaderFromMemory(Shaders.LitVs, Shaders.LitFs);
        litLightDir = Raylib.GetShaderLocation(lit, "lightDir");
        cutout = Raylib.LoadShaderFromMemory(Shaders.BasicVs, Shaders.CutoutFs);
        models = new ModelLibrary(lit);
        camera = new Camera3D { Up = Vector3.UnitY, FovY = 40, Projection = CameraProjection.Perspective };
    }

    /// <summary>The game changed floor or started: no glide to the new place.</summary>
    public void Snap() => snapped = false;

    /// <summary>1 = the classic screen, up to 2 = twice as far (wheel up = nearer).</summary>
    public float ZoomLevel { get => zoom; set => zoom = Math.Clamp(value, 1f, 2f); }

    public void Zoom(float wheel) => ZoomLevel = zoom * MathF.Pow(0.9f, wheel);

    /// <summary>
    /// The cells shown in full: the 32x24 screen of the game, larger by the zoom.
    /// Columns x0..x1, rows y0..y1, may go outside the map.
    /// </summary>
    public (int X0, int Y0, int X1, int Y1) Shown(Core.Game g)
    {
        int fx = g.FrameX > 128 ? g.FrameX - 256 : g.FrameX;
        int fy = g.FrameY > 128 ? g.FrameY - 256 : g.FrameY;
        int ex = (int)MathF.Round(Core.Game.ScreenW * (zoom - 1) / 2), ey = (int)MathF.Round(Core.Game.ScreenH * (zoom - 1) / 2);
        return (fx - ex, fy - ey, fx + Core.Game.ScreenW - 1 + ex, fy + Core.Game.ScreenH - 1 + ey);
    }

    public void Draw(Core.Game g, float dt)
    {
        time += dt;
        // the camera glides to the frame of the game (the jumps of 1983 become a slide)
        var want = new Vector2(g.FrameX + 16f, g.FrameY + 12f);
        if (g.FrameX > 128) want.X -= 256;
        if (g.FrameY > 128) want.Y -= 256;
        // the 3D view shows more than 32x24 cells: keep the camera over the map
        want = Vector2.Clamp(want, new Vector2(14, 10), new Vector2(114, 103));
        if (!snapped || Vector2.Distance(want, target) > 60) { target = want; snapped = true; }
        else target += (want - target) * (1 - MathF.Exp(-dt * 10));
        var (pitch, dist) = Presets[Math.Clamp(Preset, 0, Presets.Length - 1)];
        dist *= zoom;
        float a = pitch * MathF.PI / 180;
        camera.Target = new Vector3(target.X, 0, target.Y);
        camera.Position = camera.Target + new Vector3(0, MathF.Sin(a) * dist, MathF.Cos(a) * dist);
        // the cards stand up along the vertical of the screen
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        cardUp = Vector3.Cross(right, forward);
        cardRight = right;

        thingAt.Clear();
        foreach (var t in g.Things)
            if (!t.Dead) thingAt[t.Pos] = t;
        projectileAt.Clear();
        foreach (var p in g.Projectiles)
            if (p.Life > 0) projectileAt[p.Pos] = p;

        var light = Vector3.Normalize(new Vector3(-0.4f, -1f, -0.6f));
        Raylib.SetShaderValue(lit, litLightDir, light, ShaderUniformDataType.Vec3);

        Raylib.ClearBackground(Color.Black);
        // the depth buffer spread over what is there (raylib's default 0.01-1000 wastes it,
        // and the cards lying almost flat on the floor flicker)
        Rlgl.SetClipPlanes(1, 200);
        Raylib.BeginMode3D(camera);

        var (sx0, sy0, sx1, sy1) = Shown(g);
        bool Inside(int col, int row) => col >= sx0 && col <= sx1 && row >= sy0 && row <= sy1;
        int mx = (int)(6 * zoom), my = (int)(8 * zoom);
        int x0 = Math.Max(0, sx0 - mx), x1 = Math.Min(127, sx1 + mx);
        int y0 = Math.Max(0, sy0 - my), y1 = Math.Min(112, sy1 + my);

        // the solid things: floor, walls, doors, stairs, and the models
        for (int row = y0; row <= y1; row++)
            for (int col = x0; col <= x1; col++)
            {
                bool inside = Inside(col, row);
                DrawSolid(g, col, row, g.Shown(col, row), inside ? 1f : 0.3f, inside);
            }
        DrawMan(g, cards: false);

        // the cards with the characters (alpha cut out, so no sorting)
        Raylib.BeginShaderMode(cutout);
        for (int row = Math.Max(y0, sy0); row <= Math.Min(y1, sy1); row++)
            for (int col = Math.Max(x0, sx0); col <= Math.Min(x1, sx1); col++)
                DrawCard(g, col, row, g.Shown(col, row));
        for (int row = y0; row <= y1; row++)
            for (int col = x0; col <= x1; col++)
            {
                int c = g.Shown(col, row);
                if (Codes.IsText(c)) Decal(col, row, c, Inside(col, row) ? 1f : 0.3f);
            }
        DrawMan(g, cards: true);
        Raylib.EndShaderMode();

        // the force fields, see-through, last
        for (int row = y0; row <= y1; row++)
            for (int col = x0; col <= x1; col++)
                if (g.Shown(col, row) == Codes.ForceField) ForceField(col, row);

        Raylib.EndMode3D();
    }

    // ---------------- solid geometry ----------------

    static Color Shade(Color c, float k) =>
        new((byte)Math.Clamp(c.R * k, 0, 255), (byte)Math.Clamp(c.G * k, 0, 255), (byte)Math.Clamp(c.B * k, 0, 255), c.A);

    /// <summary>A box resting on the floor, its faces lit by hand (all in one batch).</summary>
    static void Box(float cx, float cz, float sx, float sy, float sz, Color c, float y = 0)
    {
        float x0 = cx - sx / 2, x1 = cx + sx / 2, z0 = cz - sz / 2, z1 = cz + sz / 2, y1 = y + sy;
        Rlgl.Begin(DrawMode.Quads);
        // top
        Rlgl.Color4ub(c.R, c.G, c.B, c.A);
        Rlgl.Vertex3f(x0, y1, z0); Rlgl.Vertex3f(x0, y1, z1); Rlgl.Vertex3f(x1, y1, z1); Rlgl.Vertex3f(x1, y1, z0);
        // south (towards the camera)
        var s = Shade(c, 0.78f);
        Rlgl.Color4ub(s.R, s.G, s.B, s.A);
        Rlgl.Vertex3f(x0, y1, z1); Rlgl.Vertex3f(x0, y, z1); Rlgl.Vertex3f(x1, y, z1); Rlgl.Vertex3f(x1, y1, z1);
        // north
        s = Shade(c, 0.5f);
        Rlgl.Color4ub(s.R, s.G, s.B, s.A);
        Rlgl.Vertex3f(x1, y1, z0); Rlgl.Vertex3f(x1, y, z0); Rlgl.Vertex3f(x0, y, z0); Rlgl.Vertex3f(x0, y1, z0);
        // west
        s = Shade(c, 0.62f);
        Rlgl.Color4ub(s.R, s.G, s.B, s.A);
        Rlgl.Vertex3f(x0, y1, z0); Rlgl.Vertex3f(x0, y, z0); Rlgl.Vertex3f(x0, y, z1); Rlgl.Vertex3f(x0, y1, z1);
        // east
        s = Shade(c, 0.7f);
        Rlgl.Color4ub(s.R, s.G, s.B, s.A);
        Rlgl.Vertex3f(x1, y1, z1); Rlgl.Vertex3f(x1, y, z1); Rlgl.Vertex3f(x1, y, z0); Rlgl.Vertex3f(x1, y1, z0);
        Rlgl.End();
    }

    static void FloorTile(int col, int row, Color c, float y = 0)
    {
        Rlgl.Begin(DrawMode.Quads);
        Rlgl.Color4ub(c.R, c.G, c.B, c.A);
        float x0 = col + 0.03f, x1 = col + 0.97f, z0 = row + 0.03f, z1 = row + 0.97f;
        Rlgl.Vertex3f(x0, y, z0); Rlgl.Vertex3f(x0, y, z1); Rlgl.Vertex3f(x1, y, z1); Rlgl.Vertex3f(x1, y, z0);
        Rlgl.End();
    }

    const float CardHeight = 0.08f;

    static readonly Color FloorColour = new(26, 26, 34, 255);
    static readonly Color StairColour = new(0, 0, 150, 255);

    void DrawSolid(Core.Game g, int col, int row, int c, float light, bool inside)
    {
        if (c == Codes.OutOfMap) return;
        var at = new Vector3(col + 0.5f, 0, row + 0.5f);
        // the floor under everything
        if (models.TryGet("floor", out var floor)) ModelLibrary.Draw(floor, at, 0, Shade(Color.White, light));
        else FloorTile(col, row, Shade(FloorColour, light));

        if (Codes.IsWall(c) && c != Codes.ForceField) { Wall(col, row, c, light); return; }
        if (Codes.IsDoor(c))
        {
            float turn = c == Codes.DoorH ? 0 : 90;
            if (models.TryGet("door", out var door)) ModelLibrary.Draw(door, at, turn, Spectrum.Ink(c));
            else if (c == Codes.DoorH) Box(at.X, at.Z, 1f, 0.9f, 0.18f, Shade(new Color(150, 90, 30, 255), light));
            else Box(at.X, at.Z, 0.18f, 0.9f, 1f, Shade(new Color(150, 90, 30, 255), light));
            return;
        }
        if (Codes.IsStairs(c) || Codes.IsMarker(c))
        {
            bool step = c == Codes.Steps;
            if (models.TryGet(step ? "stairs_step" : "stairs_landing", out var st)) ModelLibrary.Draw(st, at, 0, Spectrum.Ink(c));
            else if (step)
            {
                // three steps going down towards the south
                for (int k = 0; k < 3; k++)
                    Box(at.X, row + 0.17f + k * 0.33f, 1f, 0.12f - k * 0.03f, 0.3f, Shade(StairColour, light * (1f - k * 0.15f)));
            }
            else FloorTile(col, row, Shade(StairColour, light * 0.7f), 0.02f);
            return;
        }
        if (!inside) return;
        DrawObjectModel(g, col, row, c, at);
    }

    /// <summary>A wall: a post and arms towards the walls it joins (bits: 8 up, 4 down, 2 left, 1 right).</summary>
    void Wall(int col, int row, int c, float light)
    {
        int bits = c & 15;
        // clockwise: up, right, down, left
        int mask = ((bits >> 3) & 1) | ((bits & 1) << 1) | (((bits >> 2) & 1) << 2) | (((bits >> 1) & 1) << 3);
        var at = new Vector3(col + 0.5f, 0, row + 0.5f);
        var ink = Shade(Spectrum.Ink(c), light);
        if (WallModel(mask, out var m, out float turn)) { ModelLibrary.Draw(m!, at, turn, ink); return; }
        const float t = 0.42f, h = 1.0f;
        Box(at.X, at.Z, t, h, t, ink);
        float arm = (1 - t) / 2;
        if ((mask & 1) != 0) Box(at.X, row + arm / 2, t, h, arm, ink);
        if ((mask & 4) != 0) Box(at.X, row + 1 - arm / 2, t, h, arm, ink);
        if ((mask & 8) != 0) Box(col + arm / 2, at.Z, arm, h, t, ink);
        if ((mask & 2) != 0) Box(col + 1 - arm / 2, at.Z, arm, h, t, ink);
    }

    static int Rotate(int mask, int k) => ((mask << k) | (mask >> (4 - k))) & 15;

    /// <summary>The model of a wall piece: end, straight, corner, tee, cross (or post), and its turn.</summary>
    bool WallModel(int mask, out ModelLibrary.Loaded? m, out float turn)
    {
        (string key, int Base) piece = System.Numerics.BitOperations.PopCount((uint)mask) switch
        {
            0 => ("wall_post", 0),
            1 => ("wall_end", 1),
            2 => (mask is 5 or 10 ? "wall_straight" : "wall_corner", mask is 5 or 10 ? 5 : 3),
            3 => ("wall_tee", 7),
            _ => ("wall_cross", 15),
        };
        m = null; turn = 0;
        if (!models.TryGet(piece.key, out var found)) return false;
        m = found;
        for (int k = 0; k < 4; k++)
            if (Rotate(piece.Base, k) == mask) { turn = k * 90; break; }
        return true;
    }

    static float DirDegrees(int dir) => dir switch
    {
        0 => 0, 1 => 90, 2 => 180, 3 => 270, 4 => 315, 5 => 45, 6 => 135, _ => 225,
    };

    /// <summary>The model key of an object code, or null.</summary>
    static string[] ObjectKeys(int c) => c switch
    {
        Codes.GoldKey => ["key"],
        >= Codes.FirstRing and < Codes.ForceField => ["ring"],
        Codes.ArrowH or Codes.ArrowV => ["arrow"],
        Codes.Lightning or 0xA3 => ["lightning"],
        >= Codes.Fireball and < Codes.Man => ["fireball"],
        >= Codes.Treasure and < Codes.Potion => [$"treasure_{(c >> 2) & 3}", "treasure"],
        >= Codes.Potion and < Codes.Thing => [$"potion_{(c >> 2) & 3}", "potion"],
        // the disguised Thing looks like a large heap of treasure, as on the Spectrum
        Codes.Disguised => ["treasure_3", "treasure"],
        >= Codes.Thing and < Codes.ArrowBundle => [$"thing_{c & 7}", "thing"],
        >= Codes.ArrowBundle and < Codes.DoorH => ["arrow_bundle"],
        Codes.CorpseSword => ["corpse_sword", "corpse"],
        Codes.CorpseArrow => ["corpse_arrow", "corpse"],
        >= Codes.CorpseBurning and < Codes.Sword => ["corpse_burning", "corpse"],
        >= Codes.Sword and < Codes.BarEmpty => ["sword"],
        _ => [],
    };

    bool HasModel(int c, out ModelLibrary.Loaded? m)
    {
        m = null;
        foreach (var k in ObjectKeys(c))
            if (models.TryGet(k, out var f)) { m = f; return true; }
        return false;
    }

    void DrawObjectModel(Core.Game g, int col, int row, int c, Vector3 at)
    {
        if (Codes.IsMan(c) || !HasModel(c, out var m)) return;
        float turn = 0;
        var tint = Ink(g, col, row, c);
        if (Codes.IsThing(c) && thingAt.TryGetValue(row * 128 + col, out var t)) turn = DirDegrees(t.Dir);
        else if (Codes.IsProjectile(c) && projectileAt.TryGetValue(row * 128 + col, out var p))
            turn = MathF.Atan2(Step(p.Diag).X, -Step(p.Diag).Y) * 180 / MathF.PI;
        else if (c >= Codes.Sword && c < Codes.BarEmpty) turn = 90 * g.ManDir;
        if (c is >= Codes.Fireball and < Codes.Man || Codes.IsRing(c)) at.Y += 0.3f + 0.1f * MathF.Sin(time * 6);
        if (Codes.IsRing(c)) turn = time * 90;
        ModelLibrary.Draw(m!, at, turn, tint);
    }

    static Vector2 Step(int d)
    {
        int dx = ((d + 64) & 127) - 64, dy = (d - dx) / 128;
        return new Vector2(dx, dy);
    }

    // ---------------- cards ----------------

    /// <summary>A character of 1983 standing on <paramref name="foot"/> (raised by lift) and facing the camera.</summary>
    Color Ink(Core.Game g, int col, int row, int c) =>
        StrongInRed && Codes.IsThing(c) && g.StrongThingAt(col, row) ? Spectrum.Strong : Spectrum.Ink(c);

    void Card(int code, Vector3 foot, float size, float lift = 0, Color? colour = null)
    {
        var r = Spectrum.GlyphRect(code);
        var (u0, v0, u1, v1) = Uv(r);
        var half = cardRight * (size / 2);
        // a little above the floor: seen from straight above the card lies almost on it
        var bottom = foot + cardUp * lift + new Vector3(0, CardHeight, 0);
        var top = bottom + cardUp * size;
        var ink = colour ?? Spectrum.Ink(code);
        Rlgl.SetTexture(spectrum.Atlas.Id);
        Rlgl.Begin(DrawMode.Quads);
        Rlgl.Color4ub(ink.R, ink.G, ink.B, 255);
        Vertex(bottom - half, u0, v1);
        Vertex(bottom + half, u1, v1);
        Vertex(top + half, u1, v0);
        Vertex(top - half, u0, v0);
        Rlgl.End();
        Rlgl.SetTexture(0);
    }

    /// <summary>The glyph in the atlas, a little inside so that the neighbours do not bleed in.</summary>
    static (float, float, float, float) Uv(Rectangle r)
    {
        const float e = 0.02f;
        return ((r.X + e) / 128f, (r.Y + e) / 128f, (r.X + 8 - e) / 128f, (r.Y + 8 - e) / 128f);
    }

    static void Vertex(Vector3 v, float u, float t)
    {
        Rlgl.TexCoord2f(u, t);
        Rlgl.Vertex3f(v.X, v.Y, v.Z);
    }

    void DrawCard(Core.Game g, int col, int row, int c)
    {
        if (c == Codes.Space || c == Codes.OutOfMap || Codes.IsWall(c) || Codes.IsDoor(c) || Codes.IsMan(c)
            || Codes.IsStairs(c) || Codes.IsMarker(c) || Codes.IsText(c)) return;
        if (HasModel(c, out _)) return;
        float size = Codes.IsProjectile(c) ? 0.8f : 1f;
        float lift = (1 - size) / 2;
        if (c is >= Codes.Fireball and < Codes.Man || Codes.IsRing(c)) lift += 0.1f + 0.08f * MathF.Sin(time * 6);
        Card(c, new Vector3(col + 0.5f, 0, row + 1f), size, lift, Ink(g, col, row, c));
    }

    /// <summary>The man: two cells tall on the map, one figure here (head over the feet).</summary>
    void DrawMan(Core.Game g, bool cards)
    {
        int p = g.ManPos;
        var at = new Vector3((p & 127) + 0.5f, 0, (p >> 7) + 1f);
        if (models.TryGet("man", out var m))
        {
            // the warning tints the model even when it keeps its own colours
            if (!cards) ModelLibrary.Draw(m, at, 90 * g.ManDir, ManColour, force: !ManColour.Equals(Color.White));
            return;
        }
        if (!cards) return;
        int c = Codes.Man + 2 * g.ManDir;
        var foot = at with { Z = at.Z + 1 };
        Card(c + 1, foot, 1, 0, ManColour);
        Card(c, foot, 1, 1, ManColour);
    }

    /// <summary>The text of the sanctuary, painted on the floor.</summary>
    void Decal(int col, int row, int c, float light)
    {
        var r = Spectrum.GlyphRect(c);
        var (u0, v0, u1, v1) = Uv(r);
        var ink = Shade(Spectrum.Ink(c), light);
        Rlgl.SetTexture(spectrum.Atlas.Id);
        Rlgl.Begin(DrawMode.Quads);
        Rlgl.Color4ub(ink.R, ink.G, ink.B, 255);
        float y = 0.04f;
        Rlgl.TexCoord2f(u0, v0); Rlgl.Vertex3f(col, y, row);
        Rlgl.TexCoord2f(u0, v1); Rlgl.Vertex3f(col, y, row + 1);
        Rlgl.TexCoord2f(u1, v1); Rlgl.Vertex3f(col + 1, y, row + 1);
        Rlgl.TexCoord2f(u1, v0); Rlgl.Vertex3f(col + 1, y, row);
        Rlgl.End();
        Rlgl.SetTexture(0);
    }

    void ForceField(int col, int row)
    {
        if (models.TryGet("forcefield", out var m)) { ModelLibrary.Draw(m, new Vector3(col + 0.5f, 0, row + 0.5f), 0, Color.White); return; }
        byte a = (byte)(90 + 60 * MathF.Sin(time * 9 + row));
        Box(col + 0.5f, row + 0.5f, 0.25f, 1.1f, 1f, new Color((byte)0, (byte)255, (byte)255, a));
    }

    public void Dispose()
    {
        models.Dispose();
        Raylib.UnloadShader(lit);
        Raylib.UnloadShader(cutout);
    }
}
