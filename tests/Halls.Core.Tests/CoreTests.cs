using Halls.Core;

namespace Halls.Core.Tests;

public class RandomTests
{
    /// <summary>Rand1 of the Z80 (main:391) emulated register by register.</summary>
    static byte Z80Rand(byte[] seed)
    {
        static (byte v, bool c) Rl(byte v, bool c) => ((byte)((v << 1) | (c ? 1 : 0)), (v & 0x80) != 0);
        bool cy = false;
        byte l = seed[2], h = seed[3];
        (l, cy) = Rl(l, cy); (h, cy) = Rl(h, cy); (l, cy) = Rl(l, cy); (h, cy) = Rl(h, cy);
        byte c = h;
        byte a; (a, cy) = Rl(seed[0], cy);
        byte b = a;
        byte e = seed[1], d = seed[2];
        (e, cy) = Rl(e, cy); (d, cy) = Rl(d, cy);
        d &= 0x7F;
        int hl = seed[0] | seed[1] << 8;
        hl += c | b << 8;
        cy = hl > 0xFFFF;
        seed[0] = (byte)hl; seed[1] = (byte)(hl >> 8);
        hl = seed[2] | seed[3] << 8;
        hl += (e | d << 8) + (cy ? 1 : 0);
        bool minus = (hl & 0x8000) != 0;
        hl &= 0x7FFF;
        seed[2] = (byte)hl; seed[3] = (byte)(hl >> 8);
        if (!minus)
            for (int i = 0; i < 4 && ++seed[i] == 0; i++) { }
        return seed[0];
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(12345u)]
    [InlineData(0x7FFFFFFFu)]
    [InlineData(0xDEADBEEFu)]
    public void Rand_matches_the_Z80(uint s)
    {
        var r = new Z80Random(s);
        var z = BitConverter.GetBytes(s);
        for (int i = 0; i < 100_000; i++)
        {
            Assert.Equal(Z80Rand(z), r.Next());
            Assert.Equal(BitConverter.ToUInt32(z), r.Seed);
        }
    }
}

public class GameTests
{
    static Game NewGame(uint seed = 4242)
    {
        var g = new Game(seed);
        g.Start();
        return g;
    }

    [Fact]
    public void Starts_in_the_sanctuary_on_floor_0()
    {
        var g = NewGame();
        Assert.Equal(Game.StartPos, g.ManPos);
        Assert.Equal(0, g.Floor);
        Assert.Equal(0xFC, g.Map[110 * 128 + 116]);  // a landing of the stairs
        Assert.Equal((byte)'H', g.Map[2 * 128 + 117]); // "  Halls" written in the map
        Assert.Equal(GameEnd.None, g.End);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(777u)]
    [InlineData(31337u)]
    public void Every_floor_is_closed_and_all_its_cells_are_reachable(uint seed)
    {
        var g = NewGame(seed);
        for (int f = 0; f < 8; f++)
        {
            g.DebugRecreate(f);
            // the border walls
            for (int i = 0; i <= 112; i++)
            {
                Assert.True(Codes.IsWall(g.Map[i]), $"floor {f} top {i}");
                Assert.True(Codes.IsWall(g.Map[112 * 128 + i]), $"floor {f} bottom {i}");
                Assert.True(Codes.IsWall(g.Map[i * 128]), $"floor {f} left {i}");
                Assert.True(Codes.IsWall(g.Map[i * 128 + 112]), $"floor {f} right {i}");
            }
            // flood fill through everything but walls: every room centre is reached
            var seen = new bool[16384];
            var q = new Queue<int>();
            q.Enqueue(3 * 128 + 3); seen[3 * 128 + 3] = true;
            while (q.Count > 0)
            {
                int p = q.Dequeue();
                foreach (int d in new[] { 1, -1, 128, -128 })
                {
                    int n = p + d;
                    if ((n & 127) > 112 || n < 0 || n >= 113 * 128 || seen[n] || Codes.IsWall(g.Map[n])) continue;
                    seen[n] = true; q.Enqueue(n);
                }
            }
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    Assert.True(seen[(7 * y + 3) * 128 + 7 * x + 3], $"floor {f} room {x},{y}");
        }
    }

    [Fact]
    public void Floors_are_generated_again_identical()
    {
        var g = NewGame(99);
        g.DebugRecreate(3);
        var a = (byte[])g.Map.Clone();
        g.DebugRecreate(5);
        g.DebugRecreate(3);
        Assert.Equal(a, g.Map);
    }

    [Fact]
    public void Same_seed_same_inputs_same_game()
    {
        static (int, int, long, string) Play(uint seed)
        {
            var g = NewGame(seed);
            var r = new Random(1);
            Drive(g, r, 60_000);
            return (g.Wounds, g.DeadThings, g.Ticks, Convert.ToHexString(g.Map));
        }
        Assert.Equal(Play(5), Play(5));
    }

    [Theory]
    [InlineData(10u)]
    [InlineData(20u)]
    [InlineData(30u)]
    [InlineData(40u)]
    public void Random_play_runs_without_errors(uint seed)
    {
        var g = NewGame(seed);
        Drive(g, new Random((int)seed), 400_000);
        Assert.InRange(g.Wounds, 0, 400);
    }

    [Fact]
    public void The_things_shoot_the_man_left_in_the_open()
    {
        // stand still on floor 0 just past the force field: sooner or later the Things kill him
        var g = NewGame(3);
        g.DebugPlaceMan(102 * 128 + 100);
        for (int i = 0; i < 2_000_000 && g.End == GameEnd.None; i++) g.Update(1);
        Assert.Equal(GameEnd.Killed, g.End);
        Assert.True(g.Wounds >= Game.MaxWounds);
    }

    /// <summary>Random keys, a few frames at a time, at 300 cycles per second and 50 frames.</summary>
    static void Drive(Game g, Random r, int cycles)
    {
        var cmds = Enum.GetValues<Command>();
        int done = 0;
        while (done < cycles && g.End == GameEnd.None)
        {
            if (r.Next(20) == 0)
            {
                int d = r.Next(5);
                g.Left = d == 0; g.Right = d == 1; g.Up = d == 2; g.Down = d == 3;
            }
            if (r.Next(4) == 0) g.Press(cmds[r.Next(cmds.Length)]);
            g.Update(6);
            if (g.ShowingStatus) g.Press(Command.Status);
            done += 6;
        }
    }
}

public class ManTests
{
    static Game NewGame()
    {
        var g = new Game(4242, new GameOptions { ModernControls = false });
        g.Start();
        return g;
    }

    static void Hold(Game g, bool left = false, bool right = false, bool up = false, bool down = false)
    {
        g.Left = left; g.Right = right; g.Up = up; g.Down = down;
    }

    [Fact]
    public void A_step_every_10_cycles_and_turning_costs_30()
    {
        var g = NewGame();
        Assert.Equal(1, g.ManDir);           // he starts looking right
        Hold(g, left: true);
        g.Tick();                            // cycle 0: nothing due yet
        g.Tick();                            // cycle 1: he turns left
        Assert.Equal(3, g.ManDir);
        Assert.Equal(Game.StartPos, g.ManPos);
        for (int i = 0; i < 29; i++) g.Tick();
        Assert.Equal(Game.StartPos, g.ManPos); // still turning
        g.Tick();                            // cycle 31: the first step
        Assert.Equal(Game.StartPos - 1, g.ManPos);
        for (int i = 0; i < 10; i++) g.Tick();
        Assert.Equal(Game.StartPos - 2, g.ManPos);
    }

    [Fact]
    public void TapStep_a_short_press_is_one_step_a_held_one_walks_on()
    {
        var g = new Game(4242, new GameOptions { ModernControls = true, TapStep = true });
        g.Start();
        Hold(g, left: true);
        for (int i = 0; i < 32; i++) g.Tick();   // the turn, then the first step at cycle 31
        Assert.Equal(Game.StartPos - 1, g.ManPos);
        for (int i = 0; i < Game.TapDelay - 1; i++) g.Tick();
        Assert.Equal(Game.StartPos - 1, g.ManPos); // the pause after the first step
        g.Tick();
        Assert.Equal(Game.StartPos - 2, g.ManPos);
        for (int i = 0; i < 10; i++) g.Tick();
        Assert.Equal(Game.StartPos - 3, g.ManPos); // then a step every 10 cycles
        // released and pressed again: one step, and the pause again
        Hold(g);
        for (int i = 0; i < 10; i++) g.Tick();
        Hold(g, left: true);
        for (int i = 0; i < 10; i++) g.Tick();
        Assert.Equal(Game.StartPos - 4, g.ManPos);
        for (int i = 0; i < 20; i++) g.Tick();
        Assert.Equal(Game.StartPos - 4, g.ManPos);
    }

    [Fact]
    public void The_stairs_change_the_floor_on_the_left()
    {
        var g = NewGame();
        Hold(g, left: true);
        while ((g.ManPos & 127) > 116) g.Tick();
        Hold(g, up: true);
        for (int i = 0; i < 5000 && g.Floor != 1; i++) g.Tick();
        Assert.Equal(1, g.Floor);
        // the feet are on a marker of floor 1, rows 94-96
        Assert.InRange((g.ManPos >> 7) + 1, 94, 96);
        // the force field in the door of floor 1, column 112, rows 93-96
        Assert.Equal(Codes.ForceField, g.Map[94 * 128 + 112]);
    }

    [Fact]
    public void Walls_stop_the_man()
    {
        var g = NewGame();
        Hold(g, right: true);
        for (int i = 0; i < 200; i++) g.Tick();
        Assert.Equal(126, g.ManPos & 127);   // column 127 is the wall of the sanctuary
    }
}

public class StrongThingTests
{
    [Fact]
    public void On_floor_f_the_first_f_things_are_strong()
    {
        var g = new Game(4242);
        g.Start();
        for (int f = 0; f < 8; f++)
        {
            g.DebugRecreate(f);
            int strong = 0;
            for (int row = 0; row < 113; row++)
                for (int col = 0; col < 128; col++)
                    if (g.StrongThingAt(col, row)) strong++;
            int alive = g.Things.Take(f).Count(t => !t.Dead && t.Chr != Codes.Disguised);
            Assert.Equal(alive, strong);
            for (int i = 0; i < f; i++) Assert.Equal(30, g.Things[i].Reload);
        }
    }
}

public class SaveTests
{
    static void Drive(Game g, Random r, int frames)
    {
        var cmds = Enum.GetValues<Command>();
        for (int i = 0; i < frames && g.End == GameEnd.None; i++)
        {
            if (r.Next(20) == 0)
            {
                int d = r.Next(5);
                g.Left = d == 0; g.Right = d == 1; g.Up = d == 2; g.Down = d == 3;
            }
            if (r.Next(4) == 0) g.Press(cmds[r.Next(cmds.Length)]);
            g.Update(6);
            if (g.ShowingStatus) g.Press(Command.Status);
        }
    }

    static string State(Game g) =>
        $"{g.Ticks} {g.ManPos} {g.Wounds} {g.Magic} {g.ArrowCount} {g.DeadThings} {g.RingsFound} {g.Floor} {g.End} "
        + Convert.ToHexString(g.Map) + string.Join(",", g.Things.Select(t => $"{t.Pos}/{t.Hits}/{t.Count}"));

    [Theory]
    [InlineData(11u)]
    [InlineData(12u)]
    [InlineData(13u)]
    public void A_saved_game_goes_on_exactly_as_the_original(uint seed)
    {
        var a = new Game(seed);
        a.Start();
        Drive(a, new Random((int)seed), 600);
        Assert.Equal(GameEnd.None, a.End);   // saved in the middle of a game
        // the held keys belong to the front end, not to the save
        bool l = a.Left, rt = a.Right, u = a.Up, d = a.Down;

        var ms = new MemoryStream();
        a.Save(new BinaryWriter(ms));
        ms.Position = 0;
        var b = Game.Load(new BinaryReader(ms))!;
        Assert.NotNull(b);
        b.Left = l; b.Right = rt; b.Up = u; b.Down = d;
        Assert.Equal(State(a), State(b));

        Drive(a, new Random(99), 20000);
        Drive(b, new Random(99), 20000);
        Assert.True(a.Ticks > 10000);
        Assert.Equal(State(a), State(b));
    }

    [Fact]
    public void Not_a_save()
    {
        var ms = new MemoryStream(new byte[64]);
        Assert.Null(Game.Load(new BinaryReader(ms)));
    }
}
