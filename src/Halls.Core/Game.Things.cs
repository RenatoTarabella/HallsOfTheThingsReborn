namespace Halls.Core;

public sealed partial class Game
{
    /// <summary>MoveMonsters: each Thing acts when its counter reaches 0.</summary>
    void MoveThings()
    {
        foreach (var t in Things)
        {
            t.Count = (t.Count - 1) & 255;
            if (t.Count != 0) continue;
            t.Count = t.Reload;
            ThingAction(t);
            if (manDead) return;
        }
    }

    /// <summary>MMons2: one action of a Thing.</summary>
    void ThingAction(Thing t)
    {
        if (t.Dead)
        {
            // SteamRise: the burnt corpses smoke
            if (t.Chr < Codes.CorpseBurning) return;
            t.Chr = (t.Chr + 1) & 0xE3;
            Put(t.Pos, t.Chr);
            return;
        }
        if ((t.Flags & 1) != 0)
        {
            // it sees the man: no more disguise
            if (t.Chr == Codes.Disguised)
            {
                int c = (R8() & 7) | Codes.Thing;
                t.Chr = c == Codes.Disguised ? 0xD7 : c;
            }
        }
        else if (t.Chr != Codes.Disguised)
        {
            int r = R8() | (int)(rand.Seed >> 8);
            if ((r & 0x3F) == 0 && !Visible(t.Pos)) t.Chr = Codes.Disguised;
        }
        if (t.Chr != Codes.Disguised)
        {
            Put(t.Pos, Codes.Space);
            if (t.Type >= 2) FollowWall(t, t.Type == 2 ? 1 : -1);
            else Wander(t);
        }
        CheckCombat(t);
        Put(t.Pos, t.Chr);
    }

    /// <summary>MoveT2Mark.</summary>
    static void SetMark(Thing t)
    {
        t.Mark = t.Pos;
        t.WallCount = 1;
    }

    /// <summary>MonKeep: a Thing takes a potion or a treasure; true if taken.</summary>
    bool ThingKeep(Thing t, int p)
    {
        int c = M(p);
        if (Codes.IsPotion(c))
        {
            int m = t.Magic + 2 * (c & 15);
            if (m >= 256 || m >= t.MaxMagic) return false;
            t.Magic = m;
        }
        else if (Codes.IsTreasure(c))
        {
            if (t.Treasures >= 4) return false;
            t.Treasures++;
        }
        else return false;
        Put(p, Codes.Space);
        return true;
    }

    /// <summary>
    /// MonODoor: a door where the Thing goes is opened if the man is in sight
    /// beyond it, else once in four. Returns the cell where it goes.
    /// </summary>
    int ThingDoor(Thing t, int cell, int step)
    {
        cell = Wrap(cell);
        if (!Codes.IsDoor(M(cell))) return cell;
        if (Look(t.Pos, ManPos, 50, out var l) && l.Door)
        {
            OpenIfNeeded(cell, M(cell));
            SetMark(t);
            return Wrap(cell - step);
        }
        if ((R8() & 3) != 0) return cell;
        OpenIfNeeded(cell, M(cell));
        SetMark(t);
        return cell;
    }

    /// <summary>MTNewDir: blocked - keep the object, or a new direction, or follow the wall.</summary>
    void NewDirection(Thing t, int cell)
    {
        if (ThingKeep(t, cell)) return;
        t.Dir = R8() & 7;
        if (!Codes.IsWall(M(cell))) return;
        if (!t.CanFollowWall) return;
        t.Type = (R8() & 1) + 2;
        t.CanFollowWall = false;
        SetMark(t);
    }

    /// <summary>WallFC: back at the mark, the wall follower wanders again.</summary>
    void WallBack(Thing t, int cell)
    {
        if (cell != t.Mark) return;
        SetMark(t);
        t.Type = 1;
        t.CanFollowWall = true;
        t.Dir = (R8() & 3) | 4;
    }

    /// <summary>MoveThing1: wandering in 8 directions.</summary>
    void Wander(Thing t)
    {
        int step = CD1[t.Dir & 7];
        int cell = ThingDoor(t, t.Pos + step, step);
        if (M(cell) != Codes.Space) { NewDirection(t, cell); return; }
        t.Pos = cell;
        if ((R8() & 0x70) == 0) NewDirection(t, cell);
    }

    /// <summary>MoveThing2 (hand 1, right) and MoveThing3 (hand -1, left): following a wall.</summary>
    void FollowWall(Thing t, int hand)
    {
        if (t.WallCount == 0) SetMark(t);
        t.WallCount = (t.WallCount + 1) & 255;
        int dir = t.Dir;
        int step = CD[(hand == 1 ? dir : dir & 3) & 7];
        int cell = ThingDoor(t, t.Pos + step, step);
        if (M(cell) == Codes.Space)
        {
            int side = (dir + hand) & 3;
            int sStep = CD[side];
            int sCell = ThingDoor(t, t.Pos + sStep, sStep);
            if (M(sCell) == Codes.Space) { t.Pos = sCell; t.Dir = side; }
            else t.Pos = Wrap(t.Pos + CD[dir & 7]);
            WallBack(t, t.Pos);
            return;
        }
        if (ThingKeep(t, cell)) return;
        t.Dir = (dir - hand) & 3;
        WallBack(t, cell);
    }

    /// <summary>CheckCombat: the Thing sees the man - arrows, lightning, fireballs.</summary>
    void CheckCombat(Thing t)
    {
        var j = FreeProjectile();
        if (j == null) return;
        if (!Look(t.Pos, ManPos, 50, out var l) || l.Door)
        {
            t.Flags &= ~1;
            return;
        }
        virgin = true;
        t.Flags |= 1;
        // an arrow along a row or a column, more likely when near
        if (l.Minor == 0 && rand.Range(40) >= l.Major && t.Arrows != 0)
        {
            t.Arrows--;
            j.Count = j.Reload = 15;
            j.Pos = t.Pos;
            j.Diag = l.Step1;
            j.Chr = (l.Step1 & 1) ^ Codes.ArrowV;
            j.Life = 55;
            j.Back = false; j.ManFired = false; j.Target2 = -1;
            j.Kind = ProjectileKind.Arrow;
            MoveArrow(j);
        }
        int magic = t.Magic;
        if (magic < 6) return;
        if (rand.Range(12) >= magic) return;
        int r = R8() & 7;
        if (r >= 2) return;
        if (r == 0)
        {
            // lightning, in the same slot as the arrow (as on the Z80)
            InitLine(j, t.Pos, ManPos);
            j.Kind = ProjectileKind.Lightning; j.Chr = Codes.ArrowH; j.Count = j.Reload = 14; j.Life = 100;
            j.Back = false; j.ManFired = false; j.Target2 = -1;
            t.Magic = magic - 6;
            return;
        }
        if (magic < 10) return;
        j = FreeProjectile();
        if (j == null) return;
        InitLine(j, t.Pos, ManPos);
        j.Kind = ProjectileKind.Fireball; j.Chr = Codes.Fireball; j.Count = j.Reload = 24; j.Life = 50;
        j.Back = false; j.ManFired = false; j.Target = -1; j.Target2 = -1; j.TargetPos = ManPos;
        t.Magic = magic - 10;
    }

    /// <summary>HitMonster: the first Thing at p takes the wounds; corpse is its body if it dies.</summary>
    void HitThing(int p, int wounds, int corpse)
    {
        foreach (var t in Things)
        {
            if (t.Pos != p) continue;
            if (t.Hits >= wounds) { t.Hits -= wounds; return; }
            t.Dead = true;
            t.Chr = corpse;
            if (DeadThings < 255) DeadThings++;
            Put(t.Pos, corpse);
            return;
        }
    }
}
