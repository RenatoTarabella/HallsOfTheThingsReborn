namespace Halls.Core;

/// <summary>
/// Rand1 + Rand of the Z80 (main:391): the 32-bit generator of 1983, exact.
/// Every floor keeps its seed and is generated again from it, so the order
/// of the calls matters as much as the formula.
/// </summary>
public sealed class Z80Random
{
    public uint Seed;

    public Z80Random(uint seed) => Seed = seed;

    /// <summary>Rand: the next seed, returns its low byte.</summary>
    public int Next()
    {
        uint s = Seed;
        uint w1 = s >> 16;
        uint bc = (((s & 0xFF) << 1) | ((w1 >> 14) & 1)) & 0xFF;
        bc = (bc << 8) | ((w1 >> 6) & 0xFF);
        uint w0 = (s & 0xFFFF) + bc;
        uint de = (s >> 7) & 0x7FFF;
        w1 = w1 + de + (w0 >> 16);
        w0 &= 0xFFFF;
        bool positive = (w1 & 0x8000) == 0;
        w1 &= 0x7FFF;
        s = w0 | (w1 << 16);
        if (positive) s++;
        Seed = s;
        return (int)(s & 0xFF);
    }

    /// <summary>RandRange (main:1517): Rand MOD n, 0 without calling Rand when n is 0.</summary>
    public int Range(int n) => n == 0 ? 0 : Next() % n;
}
