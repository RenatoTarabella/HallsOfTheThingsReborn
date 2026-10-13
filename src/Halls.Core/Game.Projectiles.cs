namespace Halls.Core;

public sealed partial class Game
{
    /// <summary>FPS: the first free projectile, or null.</summary>
    Projectile? FreeProjectile()
    {
        foreach (var p in Projectiles)
            if (p.Life == 0) return p;
        return null;
    }

    /// <summary>
    /// RangeDoor with InitLine and NextPoint: true if the line from <paramref name="from"/>
    /// reaches <paramref name="to"/> within the range (|dx| and |dy| below it) through
    /// no wall; a door is allowed only as the first cell (then <see cref="Line.Door"/>).
    /// </summary>
    bool Look(int from, int to, int range, out Line l) => Bresenham(from, to, range, false, out l);

    /// <summary>InitLine: the projectile goes from s towards t (and on beyond).</summary>
    void InitLine(Projectile j, int s, int t)
    {
        Bresenham(s, t, 256, true, out var l);
        j.Pos = s;
        j.Diag = l.Diag; j.Step1 = l.Step1; j.Err = l.Err; j.Min = l.Minor; j.Maj = l.Major;
    }

    bool Bresenham(int from, int to, int range, bool setupOnly, out Line l)
    {
        l = default;
        int x0 = from & 127, y0 = from >> 7;
        int dx = (to & 127) - x0, dy = (to >> 7) - y0;
        int sx = Math.Sign(dx), sy = Math.Sign(dy);
        dx = Math.Abs(dx); dy = Math.Abs(dy);
        if (!setupOnly && (dx >= range || dy >= range)) return false;
        l.Diag = sx + sy * 128;
        if (dy < dx) { l.Major = dx; l.Minor = dy; l.Step1 = sx; }
        else { l.Major = dy; l.Minor = dx; l.Step1 = sy * 128; }
        l.Err = l.Major >> 1;
        if (setupOnly) return true;
        int p = from, err = l.Err;
        bool first = true;
        for (int n = l.Major; n > 0; n--)
        {
            err -= l.Minor;
            if (err < 0) { p += l.Diag; err += l.Major; }
            else p += l.Step1;
            p = Wrap(p);
            int c = Map[p];
            if (Codes.IsDoor(c))
            {
                if (!first) return false;
                l.Door = true;
                first = false;
                continue;
            }
            if (Codes.IsWall(c)) return false;
            first = false;
        }
        return true;
    }

    /// <summary>NextPoint: one step on the line of the projectile.</summary>
    static void NextPoint(Projectile j)
    {
        j.Err -= j.Min;
        int step;
        if (j.Err < 0) { step = j.Diag; j.Err += j.Maj; }
        else step = j.Step1;
        j.Pos = Wrap(j.Pos + step);
    }

    /// <summary>MoveProj: each projectile moves when its counter reaches 0.</summary>
    void MoveProjectiles()
    {
        foreach (var j in Projectiles)
        {
            if (j.Life == 0) continue;
            if (--j.Count != 0) continue;
            j.Count = j.Reload;
            StepProjectile(j);
            if (manDead) return;
        }
    }

    /// <summary>MProj2A..MProj4: one step of a projectile.</summary>
    void StepProjectile(Projectile j)
    {
        j.Life--;
        if (j.Life <= 0)
        {
            j.Life = 0;
            if (j.Back) Put(j.Pos, Codes.Space);
            return;
        }
        if (j.Back) Put(j.Pos, Codes.Space);
        switch (j.Kind)
        {
            case ProjectileKind.Arrow: MoveArrow(j); break;
            case ProjectileKind.Fireball: MoveFire(j); break;
            default: MoveLight(j); break;
        }
        if (j.Life == 0) return;
        if (M(j.Pos) != Codes.Space) { j.Back = false; return; }
        Put(j.Pos, j.Chr);
        j.Back = true;
    }

    /// <summary>MoveArrow: straight on; stops on walls and doors, 70 wounds to a Thing.</summary>
    void MoveArrow(Projectile j)
    {
        j.Pos = Wrap(j.Pos + j.Diag);
        int c = M(j.Pos);
        if (c == Codes.Space) return;
        if (Codes.IsDoor(c) || Codes.IsWall(c)) { j.Life = 0; return; }
        if (Codes.IsThing(c)) { j.Life = 0; HitThing(j.Pos, 70, Codes.CorpseArrow); return; }
        if (!Codes.IsMan(c)) return;
        int w = (j.Life + 25) >> 1;
        j.Life = 0;
        HitMan(w);
    }

    /// <summary>MFire1: the fireball ends, its Thing can be a target again.</summary>
    void EndFire(Projectile j)
    {
        j.Life = 0;
        if (j.Target2 >= 0) Things[j.Target2].Flags &= ~0x80;
    }

    /// <summary>MoveFire: it follows its target.</summary>
    void MoveFire(Projectile j)
    {
        j.Chr = ((j.Chr + 1) & 3) | Codes.Fireball;
        int target = j.Target < 0 ? ManPos : Things[j.Target].Pos;
        if (j.TargetPos != target)
        {
            j.TargetPos = target;
            InitLine(j, j.Pos, target);
        }
        NextPoint(j);
        int c = M(j.Pos);
        if (c == Codes.Space) return;
        if (Codes.IsDoor(c) || Codes.IsWall(c)) { EndFire(j); return; }
        if (Codes.IsThing(c)) { EndFire(j); HitThing(j.Pos, 40, Codes.CorpseBurning); return; }
        if (!Codes.IsMan(c)) return;
        if (j.ManFired && 50 - j.Life < 4) return;
        EndFire(j);
        HitMan(15);
    }

    static bool WallOrDoor(int c) => Codes.IsDoor(c) || Codes.IsWall(c);

    /// <summary>MLightRefl: back one step, the part of the motion that hits a wall turns.</summary>
    void Reflect(Projectile j)
    {
        j.Err = j.BErr;
        int p = j.BPos;
        j.Pos = p;
        if (WallOrDoor(M(p + j.Step1))) j.Step1 = -j.Step1;
        if (j.Min == 0) return;
        int diag = j.Diag;
        bool turned = false;
        int dy = diag < 0 ? -128 : 128;
        int dx = (diag & 64) != 0 ? -1 : 1;
        if (WallOrDoor(M(p + dy))) { diag -= 2 * dy; turned = true; }
        if (WallOrDoor(M(p + dx))) { diag -= 2 * dx; turned = true; }
        if (!turned) { diag = -diag; j.Step1 = -j.Step1; }
        j.Diag = diag;
    }

    /// <summary>MoveLight: bounces on walls and doors, goes through what it hits.</summary>
    void MoveLight(Projectile j)
    {
        j.Chr = ((j.Chr + 1) & 1) | Codes.Lightning;
        j.BErr = j.Err;
        j.BPos = j.Pos;
        int tries = 0;
        while (true)
        {
            NextPoint(j);
            if (!WallOrDoor(M(j.Pos))) break;
            Reflect(j);
            if (++tries >= 5) { j.Pos = j.BPos; return; }
        }
        int c = M(j.Pos);
        if (c == Codes.Space) return;
        if (Codes.IsThing(c)) { HitThing(j.Pos, 40, Codes.CorpseBurning); return; }
        if (!Codes.IsMan(c)) return;
        if (j.ManFired && 100 - j.Life < 4) return;
        HitMan(10);
    }
}
