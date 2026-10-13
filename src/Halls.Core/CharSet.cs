namespace Halls.Core;

/// <summary>
/// The character set of 1983 (chars.bin, loaded at #6800 on the Spectrum):
/// bytes 0-255 the attribute of each code, bytes 8*c..8*c+7 the bitmap of
/// code c (#20-#FF, top row first, bit 7 = leftmost pixel).
/// </summary>
public static class CharSet
{
    static readonly byte[] Data = Load();

    static byte[] Load()
    {
        using var s = typeof(CharSet).Assembly.GetManifestResourceStream("Halls.Core.chars.bin")
            ?? throw new InvalidOperationException("chars.bin is missing");
        var b = new byte[2048];
        s.ReadExactly(b);
        return b;
    }

    /// <summary>Spectrum attribute: bit 6 BRIGHT, bits 0-2 INK (paper is always black).</summary>
    public static byte Attribute(int code) => Data[code & 255];

    /// <summary>One row (0-7) of the glyph; codes below #20 have no bitmap.</summary>
    public static byte Row(int code, int row) => (code & 255) < 0x20 ? (byte)0 : Data[(code & 255) * 8 + row];

    /// <summary>The ink of a code as RGB, Spectrum palette (#D7 normal, #FF bright).</summary>
    public static (byte R, byte G, byte B) Ink(int code) => SpectrumColour(Attribute(code) & 7, (Attribute(code) & 0x40) != 0);

    public static (byte R, byte G, byte B) SpectrumColour(int ink, bool bright)
    {
        byte v = bright ? (byte)0xFF : (byte)0xD7;
        return ((ink & 2) != 0 ? v : (byte)0, (ink & 4) != 0 ? v : (byte)0, (ink & 1) != 0 ? v : (byte)0);
    }
}
