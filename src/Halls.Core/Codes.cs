namespace Halls.Core;

/// <summary>
/// The map is a grid of character codes (BigM): the code is both the glyph
/// and the object. Ranges as in docs/halls-analisi.md §7.3.
/// </summary>
public static class Codes
{
    public const byte Space = 0x20;
    public const byte FloorMarker = 0x80;   // #80-#87, invisible marker of floor 0-7 on the stairs
    public const byte GoldKey = 0x88;
    public const byte FirstRing = 0x89;     // #89-#8F
    public const byte ForceField = 0x90;
    public const byte ArrowH = 0xA0, ArrowV = 0xA1;
    public const byte Lightning = 0xA2;     // #A2/#A3
    public const byte Fireball = 0xA4;      // #A4-#A7
    public const byte Man = 0xA8;           // #A8-#AF: up, right, down, left (head, feet)
    public const byte Treasure = 0xB0;      // #B0-#BF
    public const byte Potion = 0xC0;        // #C0-#CF
    public const byte Thing = 0xD0;         // #D0-#D7
    public const byte Disguised = 0xD6;
    public const byte ArrowBundle = 0xD8;   // #D8-#DB, never generated
    public const byte DoorH = 0xDC, DoorV = 0xDD;
    public const byte CorpseSword = 0xDE, CorpseArrow = 0xDF;
    public const byte CorpseBurning = 0xE0; // #E0-#E3
    public const byte Sword = 0xE4;         // #E4-#E7, only on the screen
    public const byte BarEmpty = 0xE8, BarFull = 0xF0, BarLeft = 0xF1, BarRight = 0xF2;
    public const byte Landing = 0xFC, Steps = 0xFD;
    public const byte OutOfMap = 0xFF;

    public static bool IsWall(int c) => (c & 0xF0) == 0x90;          // walls #91-#9F and the force field
    public static bool IsDoor(int c) => (c & 0xFE) == DoorH;
    public static bool IsThing(int c) => (c & 0xF8) == Thing;
    public static bool IsMan(int c) => (c & 0xF8) == Man;
    public static bool IsTreasure(int c) => (c & 0xF0) == Treasure;
    public static bool IsPotion(int c) => (c & 0xF0) == Potion;
    public static bool IsRing(int c) => (c & 0xF8) == GoldKey;
    public static bool IsMarker(int c) => (c & 0xF8) == FloorMarker;
    public static bool IsStairs(int c) => (c & 0xFE) == Landing;
    public static bool IsCorpse(int c) => c is CorpseSword or CorpseArrow || (c & 0xFC) == CorpseBurning;
    public static bool IsProjectile(int c) => c >= ArrowH && c < Man;
    public static bool IsText(int c) => c > Space && c < 0x80;
}
