using Halls.Core;

namespace Halls.Game;

/// <summary>What is drawn with the characters of 1983: the map, the status, the front end.</summary>
public static class Screens
{
    /// <summary>NewScreen: the 32x24 cells of the map around the frame.</summary>
    public static void Map(Spectrum s, Core.Game g, bool strongInRed, Raylib_cs.Color man)
    {
        for (int y = 0; y < Core.Game.ScreenH; y++)
            for (int x = 0; x < Core.Game.ScreenW; x++)
            {
                int c = g.ScreenCell(x, y);
                if (strongInRed && Codes.IsThing(c) && g.StrongThingAt((g.FrameX + x) & 255, (g.FrameY + y) & 255))
                    s.Cell(x, y, c, Spectrum.Strong);
                else if (Codes.IsMan(c)) s.Cell(x, y, c, man);
                else s.Cell(x, y, c);
            }
    }

    /// <summary>PrintBar: 0-127, fills from the right (0%) to the left (100%).</summary>
    public static void Bar(Spectrum s, int x0, int y, int v)
    {
        if (v > 127) v = 127;
        s.Cell(x0 - 1, y, Codes.BarLeft);
        s.Cell(x0 + 16, y, Codes.BarRight);
        int x = x0 + 15;
        for (int n = v >> 3; n > 0; n--) s.Cell(x--, y, Codes.BarFull);
        s.Cell(x--, y, Codes.BarEmpty + (v & 7));
        while (x >= x0) s.Cell(x--, y, Codes.BarEmpty);
    }

    /// <summary>The statistics under the map (not in 1983).</summary>
    public static void Statistics(Spectrum s, Core.Game g)
    {
        s.Text(-3, 24, "Magic"); s.Text(-3, 25, "Wounds"); s.Text(-3, 26, "Arrows");
        s.Text(22, 24, "Rings"); s.Text(22, 25, "Bag"); s.Text(22, 26, "Killed");
        Bar(s, 4, 24, g.Magic >> 1);
        Bar(s, 4, 25, g.Wounds);
        Bar(s, 4, 26, g.ArrowCount >> 1);
        int x = 28;
        for (int k = 7; k >= 0; k--)
            if (g.RingTaken(k)) s.Cell(x++, 24, Codes.GoldKey + k);
        for (int k = 0; k < g.Bag.Count; k++) s.Cell(28 + k, 25, g.Bag[k]);
        s.Text(29, 26, g.DeadThings.ToString().PadLeft(3));
        s.Text(-3, 27, g.HasKey ? "Floor 0 - you have the golden key" : $"Floor {g.Floor}");
    }

    /// <summary>DisplayScore: the status screen (the game stands still).</summary>
    public static void Status(Spectrum s, Core.Game g)
    {
        s.Text(0, 0, " -- Halls of the Things --");
        s.Text(0, 1, "___________________________");
        s.Text(0, 4, "(c) Design-Design Software");
        s.Text(0, 6, "    Simon Brattel");
        s.Text(0, 7, "    Neil Mottershead");
        s.Text(0, 8, " &  Martin Horsley");
        s.Text(15, 18, "100%          0%");
        s.Text(1, 19, "Magic"); Bar(s, 15, 19, g.Magic >> 1);
        s.Text(1, 20, "Wounds"); Bar(s, 15, 20, g.Wounds);
        s.Text(1, 21, "Dead Things"); Bar(s, 15, 21, g.DeadThings >> 1);
        s.Text(1, 22, "Arrows"); Bar(s, 15, 22, g.ArrowCount >> 1);
        s.Text(1, 15, "Rings found");
        int l = 15;
        for (int k = 7; k >= 0; k--)
            if (g.RingTaken(k)) s.Cell(l++, 15, Codes.GoldKey + k);
    }

    /// <summary>Screen1: the instructions and the high score.</summary>
    public static void Front(Spectrum s, Settings st, int models, int modelErrors, bool saved)
    {
        s.Text(0, 0, "Halls of the Things");
        if (st.ModernKeys)
        {
            string K(Binding b) => KeyMap.Name(KeyMap.Get(st, b));
            bool cursor = KeyMap.Get(st, Binding.Up) == Raylib_cs.KeyboardKey.Up && KeyMap.Get(st, Binding.Down) == Raylib_cs.KeyboardKey.Down
                && KeyMap.Get(st, Binding.Left) == Raylib_cs.KeyboardKey.Left && KeyMap.Get(st, Binding.Right) == Raylib_cs.KeyboardKey.Right;
            s.Text(0, 2, "MOVEMENT  " + (cursor ? "Cursor keys" : Fit($"{K(Binding.Up)} {K(Binding.Down)} {K(Binding.Left)} {K(Binding.Right)}", 22)));
            s.Text(2, 4, Fit($"Doors   {K(Binding.OpenClose)}:Open/Close", 30));
            s.Text(0, 6, Fit($"OBJECTS   {K(Binding.Keep)}:Keep  {K(Binding.Drop)}:Drop"));
            s.Text(0, 8, Fit($"WEAPONS   {K(Binding.Sword)}:Sword"));
            s.Text(2, 10, Fit($"Arrows  {K(Binding.Arrow)}:Shoot", 30));
            s.Text(10, 11, "where you look");
            s.Text(2, 13, Fit($"Spells  {K(Binding.Fireball)}:Fireball {K(Binding.Lightning)}:Lightning", 30));
            s.Text(10, 14, Fit($"{K(Binding.Heal)}:Heal", 22));
            s.Text(0, 16, Fit($"VARIOUS   {K(Binding.Status)}:Display status"));
            s.Text(10, 17, Fit($"{K(Binding.Centre)}:Centre  sh-{K(Binding.Centre)}:Follow", 22));
            s.Text(10, 18, Fit($"{K(Binding.GiveUp)}:Give up  {K(Binding.Map)}:Map", 22));
            s.Text(10, 19, Fit($"{K(Binding.Statistics)}:Statistics on/off", 22));
            s.Text(0, 21, "M: keys of 1983  K: redefine");
        }
        else
        {
            s.Text(0, 2, "MOVEMENT  N:Up"); s.Text(21, 2, "M:Down");
            s.Text(10, 3, "Z:Left"); s.Text(21, 3, "X:Right");
            s.Text(2, 4, "Doors"); s.Text(10, 4, "O:Open"); s.Text(21, 4, "C:Close");
            s.Text(0, 6, "OBJECTS"); s.Text(10, 6, "K:Keep"); s.Text(21, 6, "D:Drop");
            s.Text(0, 8, "WEAPONS"); s.Text(10, 8, "S:Sword");
            s.Text(2, 10, "Arrows  A:Shoot");
            s.Text(10, 11, "5:Left"); s.Text(21, 11, "8:Right");
            s.Text(10, 12, "7:Up"); s.Text(21, 12, "6:Down");
            s.Text(2, 14, "Spells  F:Fireball L:Lightning");
            s.Text(10, 15, "H:Heal");
            s.Text(0, 17, "VARIOUS"); s.Text(10, 17, "1:Display status");
            s.Text(9, 18, "sp:Centre sh-sp:Follow");
            s.Text(10, 19, "W:Statistics  P:Map");
            s.Text(0, 21, "TAB: modern keys");
        }
        // in 1983 at row 20, against the instructions: here in the border above, to give them room
        s.Text(0, -2, $"High Score : {st.HighName}  {st.HighScore}");
        if (saved) s.Text(0, 23, "SPACE:resume saved game  N:new", Spectrum.Ink(Codes.Treasure));
        else s.Text(0, 23, "Press SPACE to continue");
        // what is not on the Spectrum, in the border
        if (modelErrors > 0) s.Text(-4, 24, Fit($"3D: {modelErrors} model problems (models.log)", 40));
        s.Text(-4, 25, $"F2:2D/3D  F3:camera {CameraNames[Math.Clamp(st.CameraPreset, 0, 2)]}  F11:full screen");
        s.Text(-4, 26, $"+/-:speed {st.CyclesPerSecond} (in game: zoom)  F5:save");
        s.Text(-4, 27, $"R:red Things {(st.StrongInRed ? "on " : "off")}  3D models: {models}");
    }

    /// <summary>The presets of Renderer3D, from above to low.</summary>
    static readonly string[] CameraNames = ["top", "tilt", "low"];

    static string Fit(string t, int width = 32) => t.Length <= width ? t : t[..width];

    /// <summary>Redefine keys: the actions of the modern set and their keys.</summary>
    public static void Keys(Spectrum s, Settings st, int selected, bool waiting, string message)
    {
        s.Text(0, 0, "Redefine keys");
        for (int i = 0; i < KeyMap.All.Length; i++)
        {
            var (action, label, _) = KeyMap.All[i];
            string line = (label + " ").PadRight(18, '.') + " " + KeyMap.Name(KeyMap.Get(st, action));
            if (i == selected)
                s.Text(0, 2 + i, ">" + Fit(line, 31), waiting && s.Blink ? Raylib_cs.Color.Black : Spectrum.Ink(Codes.Treasure));
            else s.Text(1, 2 + i, Fit(line, 31));
        }
        if (waiting) s.Text(0, 20, "Press the new key", Spectrum.Ink(Codes.Treasure));
        else s.Text(0, 20, Fit(message));
        s.Text(0, 21, waiting ? "BACKSPACE: cancel" : "RETURN: change   R: reset all");
        if (!waiting) s.Text(0, 22, "ESCAPE: back");
        s.Text(-4, 25, "A key used by another action is");
        s.Text(-4, 26, "swapped with it.");
    }

    /// <summary>FrontScore: how it ended.</summary>
    public static int Result(Spectrum s, GameEnd end, int rings, int dead, int score)
    {
        s.Text(0, 0, end switch
        {
            GameEnd.Killed => "You have been killed",
            GameEnd.Victorious => "You are victorious !",
            _ => "You gave up , coward",
        });
        s.Text(0, 2, "You collected " + rings switch
        {
            0 => "no rings",
            1 => "one ring",
            7 or 8 => "all seven rings",
            _ => $"{rings} rings",
        });
        int y = 3;
        if (rings == 8) s.Text(0, y++, "and the golden key.");
        s.Text(0, y + 1, "You killed " + dead switch { 0 => "no monsters", 1 => "one monster", _ => $"{dead} monsters" });
        s.Text(0, y + 2, $"and scored {(score == 0 ? "no" : score.ToString())} points.");
        return y + 4;
    }
}
