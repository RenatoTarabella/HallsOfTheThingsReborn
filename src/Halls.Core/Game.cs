namespace Halls.Core;

public enum GameEnd { None = -1, Killed = 0, Victorious = 1, GaveUp = 2 }

/// <summary>Commands read on the edge of the key press (ScanKeys / ReadKey).</summary>
public enum Command
{
    Open, Close, Keep, Drop, Sword, Arrow, Fireball, Lightning, Heal, Status, Centre,
    ArrowLeft, ArrowRight, ArrowUp, ArrowDown,
}

public sealed class GameOptions
{
    /// <summary>
    /// Modern controls (as in the RISC OS version): the open key also closes,
    /// arrows fly where the man looks. False = the rules of 1983.
    /// </summary>
    public bool ModernControls { get; set; } = true;

    /// <summary>The screen follows the man instead of jumping by 28/21 cells (not in 1983).</summary>
    public bool FollowMan { get; set; }

    /// <summary>
    /// A tap of a direction is one step: after the first step the man waits
    /// <see cref="Game.TapDelay"/> cycles before walking on (not in 1983, where a
    /// step is 10 cycles and a short press easily gives two).
    /// </summary>
    public bool TapStep { get; set; }
}

/// <summary>
/// Halls of the Things (Crystal Computing / Design Design, 1983) - the game,
/// without graphics or keyboard. One call of <see cref="Tick"/> is one cycle of
/// GameLoop. Ported from the Z80 source through the BBC BASIC rewrite in
/// src/halls.bas; the Z80 labels are quoted.
/// </summary>
public sealed partial class Game
{
    public const int MapSize = 128;
    public const int ScreenW = 32, ScreenH = 24;
    public const int ManReload = 10;       // MMID
    public const int TapDelay = 30;        // TapStep: 0.1 s at 300 cycles per second
    public const int MaxWounds = 127;
    public const int MaxTreasure = 5;
    public const int StartPos = 109 * 128 + 125; // in the sanctuary, bottom right

    /// <summary>BigM: 128x128 character codes, rows 0-112 used.</summary>
    public readonly byte[] Map = new byte[MapSize * MapSize];

    /// <summary>
    /// What is drawn only on the screen and not in the map (the man on the stairs,
    /// the sword): 0 = show the map.
    /// </summary>
    public readonly byte[] Overlay = new byte[MapSize * MapSize];

    public readonly Thing[] Things = new Thing[32];
    public readonly Projectile[] Projectiles = new Projectile[32];
    public readonly GameOptions Options;

    readonly Z80Random rand;
    uint r8;                          // the refresh register of the Z80
    readonly bool[] pending = new bool[Enum.GetValues<Command>().Length];

    // the man
    public int ManPos { get; private set; }
    public int ManDir { get; private set; } = 1;   // MA: 0 up, 1 right, 2 down, 3 left
    public int ArrowDir { get; private set; } = 1; // ArrDir
    public int Wounds { get; private set; }
    public int Magic { get; private set; }
    public int ArrowCount { get; private set; }
    public int DeadThings { get; private set; }
    public int RingsFound { get; private set; }
    readonly List<int> bag = new();               // SwagBag, the last one is dropped first
    public IReadOnlyList<int> Bag => bag;
    int underMan;                     // UM: the code under the feet XOR #20
    int swordPhase, swordPos;         // SP, SW
    int newFloor;                     // NFloorFl
    long manDue, manTurnBase;
    bool manDead;
    // not in the saved game: a press, and the steps of a held key
    int aim = -1;
    bool walking;

    // the floors
    int cFloor;                       // 0-7, 8 = the golden key is taken
    public int Floor => cFloor & 7;
    public bool HasKey => cFloor == 8;
    readonly int[] ringPos = new int[8], ringFloor = new int[8];
    public bool RingTaken(int i) => ringFloor[i] == 255;
    bool virgin;                      // a Thing of this floor has seen the man

    public int FrameX { get; private set; }
    public int FrameY { get; private set; }

    /// <summary>Incremented when the whole screen is drawn again (NewScreen).</summary>
    public int ScreenVersion { get; private set; }

    public long Ticks { get; private set; }
    public GameEnd End { get; private set; } = GameEnd.None;
    public bool ShowingStatus { get; private set; }

    // keys held down (MMoveLR / MMoveUD), set by the front end every frame
    public bool Left, Right, Up, Down, Quit;

    public Game(uint seed, GameOptions? options = null)
    {
        Options = options ?? new GameOptions();
        rand = new Z80Random(seed);
        r8 = seed ^ 0x9E3779B9;
        for (int i = 0; i < 32; i++) { Things[i] = new Thing(); Projectiles[i] = new Projectile(); }
    }

    /// <summary>The seed now, to go on with the same sequence in the next game (as in 1983).</summary>
    public uint Seed => rand.Seed;

    /// <summary>ITB: InitSanctuary, InitGame, Gen8, Telep0.</summary>
    public void Start()
    {
        InitSanctuary();
        FrameX = FrameY = 255;
        bag.Clear();
        Magic = 120; ArrowCount = 255; underMan = 0;
        ManDir = 1; ArrowDir = 1; swordPhase = 0;
        Wounds = 0; DeadThings = 0; virgin = false; manDead = false;
        End = GameEnd.None;
        Gen8();
        cFloor = 0;
        Recreate(0);
        PlaceThings();
        ManPos = StartPos;
        FrameX = 125; FrameY = 109;
        if (Options.FollowMan) Follow(); else Justify(true);
        Ticks = 0;
        manDue = 1;
        Array.Clear(pending);
    }

    public void Press(Command c) => pending[(int)c] = true;

    /// <summary>Modern controls: an arrow in the given direction (0 up, 1 right, 2 down, 3 left), the man not turning.</summary>
    public void Shoot(int dir)
    {
        aim = dir & 3;
        Press(Command.Arrow);
    }

    /// <summary>Something to keep in front of the man (what K would take).</summary>
    public bool KeepableInFront()
    {
        int f = Front();
        return Keepable(M(f)) || ((ManDir & 1) != 0 && Keepable(M(f + 128)));
    }

    static bool Keepable(int c) => Codes.IsTreasure(c) || Codes.IsPotion(c) || (c & 0xFC) == Codes.ArrowBundle || Codes.IsRing(c);

    bool Take(Command c)
    {
        if (!pending[(int)c]) return false;
        pending[(int)c] = false;
        return true;
    }

    /// <summary>Runs <paramref name="cycles"/> cycles of GameLoop, then what is read once per frame.</summary>
    public void Update(int cycles)
    {
        if (End != GameEnd.None) return;
        if (ShowingStatus)
        {
            // DisplayScore: the game stands still, only H works
            if (Take(Command.Heal)) Heal();
            if (Take(Command.Status)) ShowingStatus = false;
            return;
        }
        for (int i = 0; i < cycles && End == GameEnd.None; i++) Tick();
        if (End != GameEnd.None) return;
        // JustifyScreen, TryHealing, DisplayScore, ArrowDir
        if (Take(Command.Centre)) { if (Options.FollowMan) Follow(); else Justify(false); }
        if (Take(Command.Heal)) Heal();
        if (Take(Command.Status)) ShowingStatus = true;
        if (Take(Command.ArrowRight)) ArrowDir = 1;
        if (Take(Command.ArrowLeft)) ArrowDir = 3;
        if (Take(Command.ArrowUp)) ArrowDir = 0;
        if (Take(Command.ArrowDown)) ArrowDir = 2;
    }

    /// <summary>One cycle of GameLoop: MoveMan, MoveMonsters, MoveProj.</summary>
    public void Tick()
    {
        if (End != GameEnd.None) return;
        if (manDue <= Ticks)
        {
            manTurnBase = Ticks;
            manDue = Ticks + ManReload;
            MoveMan();
        }
        if (End == GameEnd.None)
        {
            MoveThings();
            if (!manDead) MoveProjectiles();
        }
        Ticks++;
        if (manDead && End == GameEnd.None) End = GameEnd.Killed;
    }

    /// <summary>Switches between the jumps of 1983 and the screen that follows the man.</summary>
    public void SetFollow(bool follow)
    {
        Options.FollowMan = follow;
        if (follow) Follow(); else Justify(true);
    }

    /// <summary>TryHealing: 30 magic for 16-31 wounds less.</summary>
    void Heal()
    {
        if (Magic < 30) return;
        Magic -= 30;
        Wounds -= (R8() & 15) + 16;
        if (Wounds < 0) Wounds = 0;
    }

    /// <summary>DoScor / DoS1 (+ CTres for the winner).</summary>
    public int Score()
    {
        int s = DeadThings * 2 + RingsFound * 128;
        if (RingsFound == 8) s += 128;
        if (End == GameEnd.Victorious)
            foreach (var c in Map)
                if (Codes.IsTreasure(c)) s += c & 15;
        return s;
    }

    // ---------------- cells ----------------

    static int Wrap(int p) => p & 0x3FFF;

    byte M(int p) => Map[p & 0x3FFF];

    /// <summary>OutIt + the map: writes a cell of the map (and so of the screen).</summary>
    void Put(int p, int c)
    {
        p = Wrap(p);
        Map[p] = (byte)c;
        Overlay[p] = 0;
    }

    /// <summary>Writes a cell only on the screen.</summary>
    void Scr(int p, int c)
    {
        p = Wrap(p);
        Overlay[p] = Map[p] == c ? (byte)0 : (byte)c;
    }

    /// <summary>The code shown at a cell of the map (screen first).</summary>
    public int Shown(int col, int row)
    {
        if (row < 0 || row >= 113 || col < 0 || col >= 128) return Codes.OutOfMap;
        int p = row * 128 + col;
        return Overlay[p] != 0 ? Overlay[p] : Map[p];
    }

    /// <summary>
    /// A strong Thing is shown at this cell: on floor f the first f Things have magic 255
    /// and act every 30 cycles (InitMonsters, main:1455). A disguised one is not told apart.
    /// </summary>
    public bool StrongThingAt(int col, int row)
    {
        int p = row * 128 + col;
        for (int i = 0; i < Floor; i++)
        {
            var t = Things[i];
            if (!t.Dead && t.Pos == p && t.Chr != Codes.Disguised) return Codes.IsThing(Shown(col, row));
        }
        return false;
    }

    /// <summary>The code on the 32x24 screen of the Spectrum (NewSGet: X outside the map).</summary>
    public int ScreenCell(int x, int y)
    {
        int row = (FrameY + y) & 255, col = (FrameX + x) & 255;
        return Shown(col, row);
    }

    /// <summary>SCV: the position is on the screen.</summary>
    public bool Visible(int p) =>
        (((p & 127) - FrameX) & 255) < ScreenW && (((p >> 7) - FrameY) & 255) < ScreenH;

    void NewScreen() => ScreenVersion++;

    /// <summary>The refresh register of the Z80, a "poor" random byte.</summary>
    int R8()
    {
        r8 = r8 * 1103515245 + 0x3039;
        return (int)(r8 >> 16) & 255;
    }

    /// <summary>FindPosition: a random empty cell in rows and columns 0-111.</summary>
    int FindPosition()
    {
        while (true)
        {
            rand.Next();
            uint s = rand.Seed;
            if ((s & 0x7F) >= 0x70) continue;
            uint h = (s >> 8) & 0x3F;
            if (h >= 0x38) continue;
            int p = (int)((s & 0xFF) | (h << 8));
            if (Map[p] == Codes.Space) return p;
        }
    }
}

// hooks for the tests and the debug keys
public sealed partial class Game
{
    /// <summary>Saves the current floor and generates another one (as NFloor, without the man).</summary>
    internal void DebugRecreate(int f)
    {
        SaveFloor(cFloor & 7);
        cFloor = f;
        Recreate(f);
        PlaceThings();
    }

    internal void DebugSetWounds(int w) => Wounds = w;

    internal void DebugPlaceMan(int p)
    {
        Map[Wrap(ManPos)] = Codes.Space; Map[Wrap(ManPos + 128)] = Codes.Space;
        Map[Wrap(p)] = Codes.Space; Map[Wrap(p + 128)] = Codes.Space;
        ManPos = p;
        underMan = 0;
        if (Options.FollowMan) Follow(); else Justify(true);
    }
}
