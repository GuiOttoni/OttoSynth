using System;

namespace OttoSynth.Core.DSP.Effects;

public enum EffectType
{
    Reverb, Delay, Chorus, Phaser, Flanger, Distortion,
    Eq3Band, Compressor, Tremolo, BitCrusher, StereoWidener
}

public static class EffectFactory
{
    public static IEffect Create(EffectType type) => type switch
    {
        EffectType.Reverb        => new Reverb(),
        EffectType.Delay         => new Delay(),
        EffectType.Chorus        => new Chorus(),
        EffectType.Phaser        => new Phaser(),
        EffectType.Flanger       => new Flanger(),
        EffectType.Distortion    => new Distortion(),
        EffectType.Eq3Band       => new Eq3Band(),
        EffectType.Compressor    => new Compressor(),
        EffectType.Tremolo       => new Tremolo(),
        EffectType.BitCrusher    => new BitCrusher(),
        EffectType.StereoWidener => new StereoWidener(),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };
}
