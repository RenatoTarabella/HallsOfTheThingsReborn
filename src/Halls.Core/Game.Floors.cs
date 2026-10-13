namespace Halls.Core;

public sealed partial class Game
{
    /// <summary>A floor as SaveFloor keeps it: its seed, the Things, the treasures and potions.</summary>
    sealed class SavedFloor
    {
        public uint Seed;
        public readonly int[] Pos = new int[32], Chr = new int[32], Flags = new int[32];
        public readonly List<(int Pos, int Code)> Items = new();
    }

    readonly SavedFloor[] floors = Enumerable.Range(0, 8).Select(_ => new SavedFloor()).ToArray();

    /// <summary>Gen8: the eight floors, the seven rings and the golden key.</summary>
    void Gen8()
    {
        ringFloor[0] = 0;
        for (int k = 1; k < 8; k++) ringFloor[k] = rand.Range(7) + 1;
        DeadThings = 0;
        RingsFound = 0;
        for (int f = 0; f < 8; f++)
        {
            cFloor = f;
            floors[f].Seed = rand.Seed;
            GenerateMaze(f);
            PageMaze(f);
            InitThings(f);
            InitTreasure();
            SaveFloor(f);
            // DefRing: every try is written in the map, as on the Z80
            for (int k = 0; k < 8; k++)
            {
                if (ringFloor[k] != f) continue;
                int p;
                do
                {
                    p = FindPosition();
                    ringPos[k] = p;
                    Map[p] = Codes.GoldKey;
                } while ((p & 127) >= 30);
            }
        }
        cFloor = 8;
    }

    // MonsterTypes (main:4681), in the order of the calls of RandRange
    void InitThings(int floor)
    {
        foreach (var t in Things)
        {
            t.Clear();
            t.Chr = rand.Range(8) + Codes.Thing;
            t.Type = rand.Range(3);
            rand.Range(20);                       // strength, not used
            t.Magic = rand.Range(8) + 16;
            t.MaxMagic = rand.Range(16) + 32;
            rand.Range(50);                       // not used
            t.Reload = rand.Range(90) + 60;
            t.Arrows = rand.Range(3) + 4;
            rand.Range(10);                       // not used
            t.Hits = rand.Range(20) + 30;
            rand.Range(20);                       // not used
            t.Pos = FindPosition();
        }
        if (floor == 0) return;
        // the first f Things of floor f are faster and full of magic, one is very strong
        for (int i = 0; i < floor; i++) { Things[i].Magic = 255; Things[i].Reload = 30; }
        Things[floor - 1].Hits = 255;
    }

    /// <summary>InitTreasure + InitMagic: 40 treasures and 40 potions.</summary>
    void InitTreasure()
    {
        for (int k = 0; k < 40; k++)
        {
            int p = FindPosition();
            Map[p] = (byte)(Codes.Treasure | (rand.Next() & 15));
        }
        for (int k = 0; k < 40; k++)
        {
            int p = FindPosition();
            Map[p] = (byte)(Codes.Potion | (rand.Next() & 15));
        }
    }

    void SaveFloor(int f)
    {
        var s = floors[f];
        for (int i = 0; i < 32; i++)
        {
            var t = Things[i];
            s.Pos[i] = t.Pos;
            s.Chr[i] = t.Chr;
            s.Flags[i] = (t.Dead ? 0x80 : 0) | t.Type;
        }
        s.Items.Clear();
        for (int r = 0; r < 112; r++)
            for (int c = 0; c < 112; c++)
            {
                int p = r * 128 + c, code = Map[p];
                if (code >= Codes.Treasure && code < Codes.Thing) s.Items.Add((p, code));
            }
    }

    /// <summary>
    /// Recreate: the floor is generated again from its seed, then the Things and
    /// the objects saved are put back. Doors close again and the living Things
    /// find their first strength again, as in 1983.
    /// </summary>
    void Recreate(int f)
    {
        var s = floors[f];
        rand.Seed = s.Seed;
        GenerateMaze(f);
        PageMaze(f);
        InitThings(f);
        for (int i = 0; i < 32; i++)
        {
            int p = s.Pos[i];
            if (Map[p] != Codes.Space) OpenIfNeeded(p, Map[p]);
            var t = Things[i];
            t.Pos = p;
            t.Chr = s.Chr[i];
            t.Type = s.Flags[i] & 3;
            if ((s.Flags[i] & 0x80) != 0) t.Dead = true;
        }
        foreach (var (p, code) in s.Items)
        {
            if (Map[p] != Codes.Space) OpenIfNeeded(p, Map[p]);
            Map[p] = (byte)code;
        }
        foreach (var pr in Projectiles) pr.Life = 0;
        for (int i = 0; i < 8; i++)
            if (ringFloor[i] == cFloor) Map[ringPos[i]] = (byte)(Codes.GoldKey + i);
        virgin = false;
        // what was only on the screen belongs to the floor left behind
        Array.Clear(Overlay);
    }

    /// <summary>OpenIfNess: opens the door at p (all four cells).</summary>
    void OpenIfNeeded(int p, int c)
    {
        int d = c == Codes.DoorH ? 1 : c == Codes.DoorV ? 128 : 0;
        if (d == 0) return;
        do p += d; while (Codes.IsDoor(M(p)));
        for (int k = 0; k < 4; k++) { p -= d; Put(p, Codes.Space); }
    }

    /// <summary>PlaceMon: the Things (and the corpses) into the map.</summary>
    void PlaceThings()
    {
        foreach (var t in Things) Map[t.Pos] = (byte)t.Chr;
    }

    /// <summary>NFloor: the man stands on the marker of another floor.</summary>
    void ChangeFloor()
    {
        int b = newFloor - 1;
        if (cFloor == 8) return;
        if ((cFloor & 7) == b) return;
        SaveFloor(cFloor & 7);
        cFloor = b;
        Recreate(cFloor);
        PlaceThings();
        NewScreen();
        DrawMan();
    }
}
