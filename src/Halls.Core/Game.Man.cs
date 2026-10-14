namespace Halls.Core;

public sealed partial class Game
{
    // CDirect / CDirect1: up, right, down, left, then the diagonals
    static readonly int[] CD = [-128, 1, 128, -1, 0, 0, 0, 0];
    static readonly int[] CD1 = [-128, 1, 128, -1, -129, -127, 129, 127];

    // the sword (SwdUpTab..SwdLfTab): offset from the previous phase and character
    static readonly (int Off, int Chr)[][] SwordTab =
    [
        [(0, 0xE5), (-1, 0xE6), (1, 0xE5), (1, 0xE4), (-1, 0xE5)],
        [(0, 0xE4), (128, 0xE7), (0, 0xE6), (0, 0xE7), (-128, 0xE4)],
        [(0, 0xE5), (-1, 0xE4), (1, 0xE5), (1, 0xE6), (-1, 0xE5)],
        [(0, 0xE6), (128, 0xE7), (0, 0xE4), (0, 0xE7), (-128, 0xE6)],
    ];

    /// <summary>The cell in front of the man: above the head, below the feet, beside the head.</summary>
    int Front() => ManPos + CD[ManDir] + (ManDir == 2 ? CD[ManDir] : 0);

    /// <summary>PM: the man into the map (unless he stands on the stairs) and on the screen.</summary>
    void DrawMan()
    {
        int c = Codes.Man + 2 * ManDir;
        if (M(ManPos) < 0x80) { Map[Wrap(ManPos)] = (byte)c; Map[Wrap(ManPos + 128)] = (byte)(c + 1); }
        Scr(ManPos, c);
        Scr(ManPos + 128, c + 1);
    }

    /// <summary>The man in the middle of the screen (2 cells tall: rows 11 and 12).</summary>
    void Follow()
    {
        FrameX = ((ManPos & 127) - 16) & 255;
        FrameY = ((ManPos >> 7) - 11) & 255;
        NewScreen();
        DrawMan();
    }

    /// <summary>JustifyT: lines the rooms up on the screen.</summary>
    void Justify(bool force)
    {
        int nx = (7 * ((((ManPos & 127) - 1) & 255) / 7) - 8) & 255;
        int ny = (7 * ((ManPos >> 7) / 7) - 8) & 255;
        if (!force && nx == FrameX && ny == FrameY) return;
        FrameX = nx;
        FrameY = ny;
        NewScreen();
        DrawMan();
    }

    bool ManValid(int c)
    {
        if (c == Codes.Space) return true;
        if (c == Codes.ForceField) return OpenPlan();
        if (Codes.IsStairs(c)) return true;
        return Codes.IsMarker(c);
    }

    /// <summary>OpenPlan: the force field; on floor 0 it is the way out, and the end.</summary>
    bool OpenPlan()
    {
        if ((cFloor & 7) != 0) return true;
        if (!virgin) return true;
        if (cFloor == 8) End = GameEnd.Victorious;
        return false;
    }

    /// <summary>MoveMan: one step of the man, every 10 cycles.</summary>
    void MoveMan()
    {
        if (Quit) { End = GameEnd.GaveUp; return; }
        MoveDoor();
        KeepDrop();
        if (M(ManPos) != Codes.ForceField)
        {
            FireArrow();
            if (End != GameEnd.None) return;
            Fireball();
            Lightning();
            if (SwordPhase()) return;
        }
        pending[(int)Command.Sword] = pending[(int)Command.Arrow] = false;
        pending[(int)Command.Fireball] = pending[(int)Command.Lightning] = false;

        // MMoveLR has priority over MMoveUD; both keys together = no move
        int d = -1;
        if (Left && !Right) d = 3;
        if (Right && !Left) d = 1;
        if (d < 0)
        {
            if (Up && !Down) d = 0;
            if (Down && !Up) d = 2;
        }
        if (d < 0) { walking = false; return; }
        if (d != ManDir)
        {
            // turning costs three steps
            ManDir = d;
            manDue = manTurnBase + 30;
            DrawMan();
            return;
        }
        if (underMan == 0) { Map[Wrap(ManPos)] = Codes.Space; Map[Wrap(ManPos + 128)] = Codes.Space; }
        Scr(ManPos, M(ManPos));
        Scr(ManPos + 128, M(ManPos + 128));
        int n = Wrap(ManPos + CD[ManDir]);
        newFloor = 0;
        if (!ManValid(M(n))) { DrawMan(); return; }
        if (End != GameEnd.None) return;
        int c = M(n + 128);
        if (!ManValid(c)) { DrawMan(); return; }
        if (End != GameEnd.None) return;
        if (Codes.IsMarker(c)) newFloor = (c & 7) + 1;
        underMan = c ^ 0x20;
        ManPos = n;
        // TapStep: the first step of a press, then a pause before walking on
        if (!walking && Options.TapStep) manDue = manTurnBase + TapDelay;
        walking = true;
        if (Options.FollowMan)
        {
            Follow();
            if (newFloor != 0) ChangeFloor();
            return;
        }
        if (Visible(ManPos) && Visible(ManPos + 128))
        {
            DrawMan();
            if (newFloor != 0) ChangeFloor();
        }
        else
        {
            // MManNewFrame: the screen jumps by 28 columns or 21 rows
            switch (ManDir)
            {
                case 0: FrameY = (FrameY - 21) & 255; break;
                case 1: FrameX = (FrameX + 28) & 255; break;
                case 2: FrameY = (FrameY + 21) & 255; break;
                default: FrameX = (FrameX - 28) & 255; break;
            }
            NewScreen();
            DrawMan();
        }
    }

    /// <summary>MMoveDoor: O opens the door in front, C closes a gap or an open door.</summary>
    void MoveDoor()
    {
        bool close = Take(Command.Close);
        int h = 0;
        if (Take(Command.Open))
        {
            h = Front();
            // modern controls: the same key closes when there is no door in front
            if (Options.ModernControls && !Codes.IsDoor(M(h))) close = true;
        }
        if (!close && h != 0)
        {
            if (!Codes.IsDoor(M(h))) return;
            int d = CD[(ManDir + 1) & 3];
            do h += d; while (Codes.IsDoor(M(h)));
            for (int k = 0; k < 4; k++) { h -= d; Put(h, Codes.Space); }
            return;
        }
        if (!close) return;
        h = Front();
        int s = CD[(ManDir + 1) & 3];
        if (M(h) != Codes.Space) return;
        do h += s; while (M(h) == Codes.Space);
        if (!Codes.IsWall(M(h))) return;
        for (int k = 0; k < 4; k++)
        {
            h -= s;
            if (M(h) != Codes.Space) return;
        }
        h -= s;
        if (!Codes.IsWall(M(h))) return;
        int door = s is 1 or -1 ? Codes.DoorH : Codes.DoorV;
        for (int k = 0; k < 4; k++) { h += s; Put(h, door); }
    }

    /// <summary>MMKeepDrop: K keeps what is in front, D drops the last treasure.</summary>
    void KeepDrop()
    {
        if (Take(Command.Drop))
        {
            if (bag.Count == 0) return;
            int h = Front();
            if (M(h) != Codes.Space)
            {
                if ((ManDir & 1) == 0) return;
                h += 128;
                if (M(h) != Codes.Space) return;
            }
            Put(h, bag[^1]);
            bag.RemoveAt(bag.Count - 1);
            return;
        }
        if (!Take(Command.Keep)) return;
        int f = Front();
        if (Keep(f) && (ManDir & 1) != 0) Keep(f + 128);
    }

    /// <summary>MMKeep2: true when there was nothing to keep.</summary>
    bool Keep(int h)
    {
        int c = M(h);
        if (Codes.IsTreasure(c))
        {
            if (bag.Count == MaxTreasure) return false;
            bag.Add(c);
            Put(h, Codes.Space);
            return false;
        }
        if (Codes.IsPotion(c))
        {
            int n = Magic + 2 * (c & 15);
            if (n > 255) return true;
            if (n == 255) return false;
            Magic = n;
            Put(h, Codes.Space);
            return false;
        }
        if ((c & 0xFC) == Codes.ArrowBundle) { Put(h, Codes.Space); ArrowCount = (ArrowCount + 1) & 255; return false; }
        if (Codes.IsRing(c)) { TakeRing(h, c); return false; }
        return true;
    }

    /// <summary>MMKRing: a ring or the golden key.</summary>
    void TakeRing(int h, int c)
    {
        ringFloor[c & 7] = 255;
        Put(h, Codes.Space);
        if (c == Codes.GoldKey) cFloor = 8;
        RingsFound++;
        if (RingsFound != 7) return;
        // GoldKey: the door of floor 0 opens in the sanctuary
        for (int k = 0; k < 6; k++) Put((104 - k) * 128 + 113, M((97 - k) * 128 + 113));
    }

    /// <summary>MSword: five phases, one per step; true while the man must stand.</summary>
    bool SwordPhase()
    {
        if (swordPhase == 0)
        {
            if (!Take(Command.Sword)) return false;
            swordPhase = 6;
            swordPos = Front();
        }
        int e = 6 - swordPhase;
        Scr(swordPos, M(swordPos));
        swordPhase--;
        if (swordPhase == 0) return false;
        var (off, chr) = SwordTab[ManDir][e];
        swordPos = Wrap(swordPos + off);
        int c = M(swordPos);
        if (Codes.IsDoor(c)) { swordPhase = 0; return false; }
        if (Codes.IsThing(c)) { swordPhase = 0; HitThing(swordPos, 15, Codes.CorpseSword); return false; }
        if (Codes.IsWall(c)) { swordPhase = 0; return false; }
        Scr(swordPos, chr);
        return true;
    }

    /// <summary>
    /// MFireArrow: the arrow leaves from the head or the feet, towards the
    /// nearest Thing in front (the scan of the Z80, with its asymmetry).
    /// </summary>
    void FireArrow()
    {
        if (!Take(Command.Arrow)) return;
        int a = aim;
        aim = -1;
        var j = FreeProjectile();
        if (j == null) return;
        if (ArrowCount == 0) return;
        ArrowCount--;
        if (Options.ModernControls) ArrowDir = a >= 0 ? a : ManDir;
        int h = ManPos, de = CD[ArrowDir];
        if (ArrowDir == 2) h += de;
        if ((ArrowDir & 1) != 0)
        {
            int ho = -128, fo = 0, k = ManPos + 128, res = 0, n = 0;
            bool blocked = false;
            do
            {
                k += de; n++;
                int c = M(k + ho);
                if (Codes.IsDoor(c) || Codes.IsThing(c)) res = 2;
                if (res == 0 && Codes.IsWall(c))
                {
                    if (blocked) res = 2;
                    blocked = true; ho = 0;
                }
                if (res == 0)
                {
                    c = M(k + fo);
                    if (Codes.IsDoor(c)) res = 2;
                    if (res == 0 && Codes.IsThing(c)) res = 1;
                    if (res == 0 && Codes.IsWall(c))
                    {
                        if (blocked) res = 2;
                        blocked = true; fo = -128;
                    }
                }
            } while (res == 0 && n != 256);
            if (res == 1) h += 128;
        }
        j.Kind = ProjectileKind.Arrow; j.Diag = de; j.Pos = Wrap(h); j.Back = false; j.Life = 32;
        j.Chr = (ArrowDir & 1) != 0 ? Codes.ArrowH : Codes.ArrowV;
        j.Count = j.Reload = 5; j.ManFired = true; j.Target2 = -1;
        StepProjectile(j);
    }

    /// <summary>MonsterSearch: the nearest Thing in sight within range, not already targeted.</summary>
    int SearchThing(int range)
    {
        int b = -1, best = 255;
        for (int i = 0; i < 32; i++)
        {
            var t = Things[i];
            if (t.Dead || (t.Flags & 0x80) != 0) continue;
            if (Look(ManPos, t.Pos, range, out var l) && !l.Door && l.Major <= best) { best = l.Major; b = i; }
        }
        return b;
    }

    /// <summary>FireBall: 2 magic, even with no target.</summary>
    void Fireball()
    {
        if (!Take(Command.Fireball)) return;
        var j = FreeProjectile();
        if (j == null || Magic < 2) return;
        Magic -= 2;
        int i = SearchThing(40);
        if (i < 0) return;
        var t = Things[i];
        t.Flags |= 0x80;
        InitLine(j, ManPos, t.Pos);
        j.Kind = ProjectileKind.Fireball; j.Chr = Codes.Fireball; j.Count = j.Reload = 24; j.Back = false; j.Life = 50;
        j.ManFired = true; j.Target = i; j.TargetPos = t.Pos; j.Target2 = i;
    }

    /// <summary>Lightning: 1 magic, even with no target.</summary>
    void Lightning()
    {
        if (!Take(Command.Lightning)) return;
        var j = FreeProjectile();
        if (j == null || Magic < 1) return;
        Magic -= 1;
        int i = SearchThing(50);
        if (i < 0) return;
        InitLine(j, ManPos, Things[i].Pos);
        j.Kind = ProjectileKind.Lightning; j.Chr = Codes.Lightning; j.Count = j.Reload = 14; j.Back = false; j.Life = 100;
        j.ManFired = true; j.Target2 = -1;
    }

    /// <summary>HitMan: at 127 wounds the man is dead.</summary>
    void HitMan(int wounds)
    {
        Wounds += wounds;
        if (Wounds >= MaxWounds) manDead = true;
    }
}
