using Halls.Core;
using Raylib_cs;

namespace Halls.Game;

/// <summary>
/// The man near death (not in 1983): below 20% of life he fades from white to
/// bright red and back, faster and faster as the wounds grow.
/// </summary>
public sealed class Danger
{
    /// <summary>127 wounds kill: 20% of life left is 102 wounds.</summary>
    public const int Threshold = Core.Game.MaxWounds - Core.Game.MaxWounds / 5;
    const float SlowHz = 1f, FastHz = 6f;

    float phase;

    /// <summary>The colour of the man for this frame.</summary>
    public Color Colour { get; private set; } = Color.White;

    public void Update(Core.Game g, float dt)
    {
        if (g.Wounds < Threshold) { phase = 0; Colour = Color.White; return; }
        // 0 at the threshold, 1 at death: the pulse speeds up linearly
        float k = Math.Clamp((g.Wounds - Threshold) / (float)(Core.Game.MaxWounds - Threshold), 0, 1);
        // the phase runs on with the speed, so the pulse never jumps
        phase = (phase + dt * (SlowHz + (FastHz - SlowHz) * k)) % 1;
        float red = (1 - MathF.Cos(phase * 2 * MathF.PI)) / 2;
        byte gb = (byte)(255 * (1 - red));
        Colour = new Color((byte)255, gb, gb, (byte)255);
    }
}
