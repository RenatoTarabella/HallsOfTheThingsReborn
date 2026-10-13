namespace Halls.Core;

/// <summary>A Thing (a record of ML, 64 bytes on the Z80; field offsets in the comments).</summary>
public sealed class Thing
{
    public int Pos;        // 0-1 position in the map
    public int Chr;        // 2 character
    public int Type;       // 4 movement: 0/1 wandering, 2 right hand, 3 left hand
    public bool Dead;      // 7 bit 7
    public int Magic;      // 8
    public int MaxMagic;   // 9
    public int Treasures;  // 10
    public int Count;      // 16 delay counter
    public int Reload;     // 18 cycles per action
    public int Arrows;     // 20
    public int Hits;       // 22 hit points
    public int Flags;      // 26 bit 0 sees the man, bit 7 target of a fireball
    public int Dir;        // 27 direction 0-7
    public int Mark;       // 32-33 wall-follower mark
    public int WallCount;  // 34
    public bool CanFollowWall; // 35

    public void Clear()
    {
        Pos = Chr = Type = Magic = MaxMagic = Treasures = Count = Reload = Arrows = Hits = Flags = Dir = Mark = WallCount = 0;
        Dead = CanFollowWall = false;
    }
}

public enum ProjectileKind { None = 0, Arrow = 1, Fireball = 2, Lightning = 3 }

/// <summary>A projectile (a record of PL, 32 bytes on the Z80).</summary>
public sealed class Projectile
{
    public int Count, Reload;  // it moves every Reload cycles
    public int Pos;
    public int Diag;           // step of an arrow, diagonal step of a line
    public ProjectileKind Kind;
    public int Life;           // 0 = free slot
    public int Chr;
    public bool Back;          // the character is in the map
    public int Step1, Err, Min, Maj; // Bresenham
    public bool ManFired;
    public int Target = -1;    // fireball: Thing index, -1 = the man
    public int TargetPos;
    public int Target2 = -1;   // Thing marked as target by the man's fireball
    public int BErr, BPos;     // lightning: state before the step
}

/// <summary>The result of RangeDoor / InitLine (the work area of ws 20-40).</summary>
public struct Line
{
    public bool Door;   // the first cell is a door
    public int Minor, Step1, Major, Diag, Err;
}
