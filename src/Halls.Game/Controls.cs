using Halls.Core;
using Raylib_cs;

namespace Halls.Game;

/// <summary>What a key of the modern set does (all can be redefined).</summary>
public enum Binding
{
    Up, Down, Left, Right, OpenClose, Keep, Drop, Sword, Arrow, Fireball, Lightning, Heal,
    Status, Centre, Map, Statistics, GiveUp,
}

/// <summary>The keys of the modern set: one per action, kept in the settings, redefinable.</summary>
public static class KeyMap
{
    public static readonly (Binding Action, string Label, KeyboardKey Default)[] All =
    [
        (Binding.Up, "Up", KeyboardKey.Up),
        (Binding.Down, "Down", KeyboardKey.Down),
        (Binding.Left, "Left", KeyboardKey.Left),
        (Binding.Right, "Right", KeyboardKey.Right),
        (Binding.OpenClose, "Open/Close door", KeyboardKey.Enter),
        (Binding.Keep, "Keep", KeyboardKey.E),
        (Binding.Drop, "Drop", KeyboardKey.Q),
        (Binding.Sword, "Sword", KeyboardKey.LeftControl),
        (Binding.Arrow, "Arrow", KeyboardKey.Space),
        (Binding.Fireball, "Fireball", KeyboardKey.F),
        (Binding.Lightning, "Lightning", KeyboardKey.L),
        (Binding.Heal, "Heal", KeyboardKey.H),
        (Binding.Status, "Display status", KeyboardKey.Tab),
        (Binding.Centre, "Centre", KeyboardKey.C),
        (Binding.Map, "Map", KeyboardKey.P),
        (Binding.Statistics, "Statistics", KeyboardKey.S),
        (Binding.GiveUp, "Give up", KeyboardKey.Escape),
    ];

    public static KeyboardKey Default(Binding b) => All.First(a => a.Action == b).Default;

    public static KeyboardKey Get(Settings s, Binding b) =>
        s.Keys.TryGetValue(b.ToString(), out var name) && Enum.TryParse<KeyboardKey>(name, out var k) && Allowed(k)
            ? k : Default(b);

    /// <summary>Gives the key to the action; an action that had it gets the old key of this one.</summary>
    /// <returns>The action that was swapped, if any.</returns>
    public static Binding? Set(Settings s, Binding b, KeyboardKey key)
    {
        var old = Get(s, b);
        Binding? other = null;
        foreach (var (a, _, _) in All)
            if (a != b && Get(s, a) == key) { other = a; s.Keys[a.ToString()] = old.ToString(); }
        s.Keys[b.ToString()] = key.ToString();
        return other;
    }

    public static void Reset(Settings s) => s.Keys.Clear();

    /// <summary>The F keys are the game's own, shift makes "follow", backspace cancels.</summary>
    public static bool Allowed(KeyboardKey k) =>
        k != KeyboardKey.Null && !(k >= KeyboardKey.F1 && k <= KeyboardKey.F12)
        && k is not (KeyboardKey.LeftShift or KeyboardKey.RightShift or KeyboardKey.Backspace);

    public static string Name(KeyboardKey k) => k switch
    {
        >= KeyboardKey.A and <= KeyboardKey.Z => ((char)k).ToString(),
        >= KeyboardKey.Zero and <= KeyboardKey.Nine => ((char)k).ToString(),
        >= KeyboardKey.Kp0 and <= KeyboardKey.Kp9 => "KP" + (k - KeyboardKey.Kp0),
        KeyboardKey.Up => "UP", KeyboardKey.Down => "DOWN", KeyboardKey.Left => "LEFT", KeyboardKey.Right => "RIGHT",
        KeyboardKey.Enter => "RETURN", KeyboardKey.KpEnter => "KP RETURN", KeyboardKey.Space => "SPACE",
        KeyboardKey.LeftControl => "CTRL", KeyboardKey.RightControl => "R-CTRL",
        KeyboardKey.LeftAlt => "ALT", KeyboardKey.RightAlt => "ALT GR",
        KeyboardKey.Tab => "TAB", KeyboardKey.Escape => "ESCAPE", KeyboardKey.CapsLock => "CAPS LOCK",
        KeyboardKey.Insert => "INSERT", KeyboardKey.Delete => "DELETE", KeyboardKey.Home => "HOME", KeyboardKey.End => "END",
        KeyboardKey.PageUp => "PAGE UP", KeyboardKey.PageDown => "PAGE DOWN",
        KeyboardKey.KpAdd => "KP +", KeyboardKey.KpSubtract => "KP -", KeyboardKey.KpMultiply => "KP *",
        KeyboardKey.KpDivide => "KP /", KeyboardKey.KpDecimal => "KP .",
        KeyboardKey.Comma => ",", KeyboardKey.Period => ".", KeyboardKey.Minus => "-", KeyboardKey.Equal => "=",
        KeyboardKey.Semicolon => ";", KeyboardKey.Apostrophe => "'", KeyboardKey.Slash => "/",
        KeyboardKey.Backslash => "\\", KeyboardKey.LeftBracket => "[", KeyboardKey.RightBracket => "]",
        KeyboardKey.Grave => "`",
        _ => k.ToString().ToUpperInvariant(),
    };
}

/// <summary>The keys of 1983 and the modern ones (redefinable, see <see cref="KeyMap"/>).</summary>
public static class Controls
{
    static readonly (KeyboardKey Key, Command Cmd)[] Keys1983 =
    [
        (KeyboardKey.O, Command.Open), (KeyboardKey.C, Command.Close),
        (KeyboardKey.K, Command.Keep), (KeyboardKey.D, Command.Drop),
        (KeyboardKey.S, Command.Sword), (KeyboardKey.A, Command.Arrow),
        (KeyboardKey.F, Command.Fireball), (KeyboardKey.L, Command.Lightning),
        (KeyboardKey.H, Command.Heal), (KeyboardKey.One, Command.Status),
        (KeyboardKey.Space, Command.Centre),
        (KeyboardKey.Five, Command.ArrowLeft), (KeyboardKey.Eight, Command.ArrowRight),
        (KeyboardKey.Seven, Command.ArrowUp), (KeyboardKey.Six, Command.ArrowDown),
    ];

    static readonly (Binding Action, Command Cmd)[] ModernCommands =
    [
        (Binding.OpenClose, Command.Open), (Binding.Keep, Command.Keep), (Binding.Drop, Command.Drop),
        (Binding.Sword, Command.Sword), (Binding.Arrow, Command.Arrow), (Binding.Fireball, Command.Fireball),
        (Binding.Lightning, Command.Lightning), (Binding.Heal, Command.Heal), (Binding.Status, Command.Status),
        (Binding.Centre, Command.Centre),
    ];

    public static bool Shift => Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);

    static bool Down(KeyboardKey k) => Raylib.IsKeyDown(k);

    /// <summary>Held keys and pressed commands of this frame into the game.</summary>
    public static void Read(Halls.Core.Game g, Settings s)
    {
        if (!s.ModernKeys)
        {
            // Z X N M as on the Spectrum, and the cursor keys
            g.Left = Down(KeyboardKey.Left) || Down(KeyboardKey.Z);
            g.Right = Down(KeyboardKey.Right) || Down(KeyboardKey.X);
            g.Up = Down(KeyboardKey.Up) || Down(KeyboardKey.N);
            g.Down = Down(KeyboardKey.Down) || Down(KeyboardKey.M);
            g.Quit = Down(KeyboardKey.Q) && Down(KeyboardKey.T) && Down(KeyboardKey.U) && Down(KeyboardKey.I);
            foreach (var (key, cmd) in Keys1983)
            {
                // shift + centre switches the following screen, handled by the app
                if (cmd == Command.Centre && Shift) continue;
                if (Raylib.IsKeyPressed(key)) g.Press(cmd);
            }
            return;
        }
        g.Left = Down(KeyMap.Get(s, Binding.Left));
        g.Right = Down(KeyMap.Get(s, Binding.Right));
        g.Up = Down(KeyMap.Get(s, Binding.Up));
        g.Down = Down(KeyMap.Get(s, Binding.Down));
        g.Quit = Down(KeyMap.Get(s, Binding.GiveUp));
        foreach (var (action, cmd) in ModernCommands)
        {
            if (cmd == Command.Centre && Shift) continue;
            if (Raylib.IsKeyPressed(KeyMap.Get(s, action))) g.Press(cmd);
        }
    }

    public static KeyboardKey CentreKey(Settings s) => s.ModernKeys ? KeyMap.Get(s, Binding.Centre) : KeyboardKey.Space;
    public static KeyboardKey StatisticsKey(Settings s) => s.ModernKeys ? KeyMap.Get(s, Binding.Statistics) : KeyboardKey.W;
    public static KeyboardKey MapKey(Settings s) => s.ModernKeys ? KeyMap.Get(s, Binding.Map) : KeyboardKey.P;
}
