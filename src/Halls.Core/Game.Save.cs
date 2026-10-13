namespace Halls.Core;

/// <summary>
/// The whole state of a game, to stop and go on later (not in 1983). Everything
/// that the next cycle reads is kept, the random generators too, so a game
/// that goes on from a save plays exactly as it would have.
/// </summary>
public sealed partial class Game
{
    const int SaveVersion = 1;
    static readonly byte[] Magic4 = "HOTT"u8.ToArray();

    public void Save(BinaryWriter w)
    {
        w.Write(Magic4);
        w.Write(SaveVersion);
        w.Write(rand.Seed);
        w.Write(r8);
        w.Write(Map);
        w.Write(Overlay);
        w.Write(rooms);
        foreach (var t in Things)
        {
            w.Write(t.Pos); w.Write(t.Chr); w.Write(t.Type); w.Write(t.Dead); w.Write(t.Magic); w.Write(t.MaxMagic);
            w.Write(t.Treasures); w.Write(t.Count); w.Write(t.Reload); w.Write(t.Arrows); w.Write(t.Hits);
            w.Write(t.Flags); w.Write(t.Dir); w.Write(t.Mark); w.Write(t.WallCount); w.Write(t.CanFollowWall);
        }
        foreach (var p in Projectiles)
        {
            w.Write(p.Count); w.Write(p.Reload); w.Write(p.Pos); w.Write(p.Diag); w.Write((int)p.Kind); w.Write(p.Life);
            w.Write(p.Chr); w.Write(p.Back); w.Write(p.Step1); w.Write(p.Err); w.Write(p.Min); w.Write(p.Maj);
            w.Write(p.ManFired); w.Write(p.Target); w.Write(p.TargetPos); w.Write(p.Target2); w.Write(p.BErr); w.Write(p.BPos);
        }
        w.Write(ManPos); w.Write(ManDir); w.Write(ArrowDir); w.Write(Wounds); w.Write(Magic); w.Write(ArrowCount);
        w.Write(DeadThings); w.Write(RingsFound);
        w.Write(bag.Count);
        foreach (int b in bag) w.Write(b);
        w.Write(underMan); w.Write(swordPhase); w.Write(swordPos); w.Write(newFloor);
        w.Write(manDue); w.Write(manTurnBase); w.Write(manDead);
        w.Write(cFloor);
        foreach (int r in ringPos) w.Write(r);
        foreach (int r in ringFloor) w.Write(r);
        w.Write(virgin);
        w.Write(FrameX); w.Write(FrameY); w.Write(Ticks); w.Write(ShowingStatus); w.Write((int)End);
        foreach (bool b in pending) w.Write(b);
        foreach (var f in floors)
        {
            w.Write(f.Seed);
            for (int i = 0; i < 32; i++) { w.Write(f.Pos[i]); w.Write(f.Chr[i]); w.Write(f.Flags[i]); }
            w.Write(f.Items.Count);
            foreach (var (p, c) in f.Items) { w.Write(p); w.Write(c); }
        }
    }

    /// <summary>A game saved by <see cref="Save"/>; null if the data is not a save of this version.</summary>
    public static Game? Load(BinaryReader r, GameOptions? options = null)
    {
        if (!r.ReadBytes(4).AsSpan().SequenceEqual(Magic4) || r.ReadInt32() != SaveVersion) return null;
        var g = new Game(0, options);
        g.rand.Seed = r.ReadUInt32();
        g.r8 = r.ReadUInt32();
        r.ReadExactly(g.Map);
        r.ReadExactly(g.Overlay);
        r.ReadExactly(g.rooms);
        foreach (var t in g.Things)
        {
            t.Pos = r.ReadInt32(); t.Chr = r.ReadInt32(); t.Type = r.ReadInt32(); t.Dead = r.ReadBoolean();
            t.Magic = r.ReadInt32(); t.MaxMagic = r.ReadInt32(); t.Treasures = r.ReadInt32(); t.Count = r.ReadInt32();
            t.Reload = r.ReadInt32(); t.Arrows = r.ReadInt32(); t.Hits = r.ReadInt32(); t.Flags = r.ReadInt32();
            t.Dir = r.ReadInt32(); t.Mark = r.ReadInt32(); t.WallCount = r.ReadInt32(); t.CanFollowWall = r.ReadBoolean();
        }
        foreach (var p in g.Projectiles)
        {
            p.Count = r.ReadInt32(); p.Reload = r.ReadInt32(); p.Pos = r.ReadInt32(); p.Diag = r.ReadInt32();
            p.Kind = (ProjectileKind)r.ReadInt32(); p.Life = r.ReadInt32(); p.Chr = r.ReadInt32(); p.Back = r.ReadBoolean();
            p.Step1 = r.ReadInt32(); p.Err = r.ReadInt32(); p.Min = r.ReadInt32(); p.Maj = r.ReadInt32();
            p.ManFired = r.ReadBoolean(); p.Target = r.ReadInt32(); p.TargetPos = r.ReadInt32(); p.Target2 = r.ReadInt32();
            p.BErr = r.ReadInt32(); p.BPos = r.ReadInt32();
        }
        g.ManPos = r.ReadInt32(); g.ManDir = r.ReadInt32(); g.ArrowDir = r.ReadInt32(); g.Wounds = r.ReadInt32();
        g.Magic = r.ReadInt32(); g.ArrowCount = r.ReadInt32(); g.DeadThings = r.ReadInt32(); g.RingsFound = r.ReadInt32();
        int n = r.ReadInt32();
        for (int i = 0; i < n; i++) g.bag.Add(r.ReadInt32());
        g.underMan = r.ReadInt32(); g.swordPhase = r.ReadInt32(); g.swordPos = r.ReadInt32(); g.newFloor = r.ReadInt32();
        g.manDue = r.ReadInt64(); g.manTurnBase = r.ReadInt64(); g.manDead = r.ReadBoolean();
        g.cFloor = r.ReadInt32();
        for (int i = 0; i < 8; i++) g.ringPos[i] = r.ReadInt32();
        for (int i = 0; i < 8; i++) g.ringFloor[i] = r.ReadInt32();
        g.virgin = r.ReadBoolean();
        g.FrameX = r.ReadInt32(); g.FrameY = r.ReadInt32(); g.Ticks = r.ReadInt64(); g.ShowingStatus = r.ReadBoolean(); g.End = (GameEnd)r.ReadInt32();
        for (int i = 0; i < g.pending.Length; i++) g.pending[i] = r.ReadBoolean();
        foreach (var f in g.floors)
        {
            f.Seed = r.ReadUInt32();
            for (int i = 0; i < 32; i++) { f.Pos[i] = r.ReadInt32(); f.Chr[i] = r.ReadInt32(); f.Flags[i] = r.ReadInt32(); }
            int k = r.ReadInt32();
            f.Items.Clear();
            for (int i = 0; i < k; i++) f.Items.Add((r.ReadInt32(), r.ReadInt32()));
        }
        // the screen may have moved with "follow" switched while away
        if (g.Options.FollowMan) g.Follow(); else g.NewScreen();
        return g;
    }
}
