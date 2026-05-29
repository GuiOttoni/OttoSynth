using System;
using System.IO;

namespace OttoSynth.Core.DSP.Oscillators;

/// <summary>
/// Loads wavetables from standard PCM WAV files without external dependencies.
///
/// Supported formats:
///   - 16-bit or 24-bit PCM, mono or stereo (stereo is down-mixed to mono)
///   - Single-cycle: exactly one frame of <see cref="BasicWavetables.DefaultTableSize"/> samples
///   - Multi-frame: N × DefaultTableSize samples in sequence (Serum / Surge / Vital format)
///
/// The output is a <c>double[frames][DefaultTableSize]</c> array normalized to [-1, 1],
/// suitable for direct use in <see cref="WavetableOscillator"/>.
/// </summary>
public static class WavetableLoader
{
    private const int FrameSize = BasicWavetables.DefaultTableSize; // 2048

    /// <summary>
    /// Loads a wavetable from a WAV file.
    /// Throws <see cref="InvalidDataException"/> if the file is not a supported PCM WAV.
    /// </summary>
    public static double[][] Load(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(fs);
        return LoadFromStream(reader, path);
    }

    /// <summary>
    /// Attempts to load a wavetable; returns null on failure (logs reason to <paramref name="error"/>).
    /// </summary>
    public static double[][]? TryLoad(string path, out string error)
    {
        try
        {
            error = string.Empty;
            return Load(path);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }

    // ─── Private implementation ──────────────────────────────────────────────

    private static double[][] LoadFromStream(BinaryReader r, string sourceName)
    {
        // ── RIFF header ──────────────────────────────────────────────────────
        string riff = ReadFourCC(r);
        if (riff != "RIFF")
            throw new InvalidDataException($"{sourceName}: not a RIFF file (got '{riff}').");

        r.ReadInt32(); // file size — ignore

        string wave = ReadFourCC(r);
        if (wave != "WAVE")
            throw new InvalidDataException($"{sourceName}: RIFF type is '{wave}', expected 'WAVE'.");

        // ── Scan chunks until we find 'fmt ' and 'data' ──────────────────────
        short numChannels = 1;
        int sampleRate = 44100;
        short bitsPerSample = 16;
        byte[]? pcmData = null;

        while (r.BaseStream.Position < r.BaseStream.Length - 8)
        {
            string chunkId   = ReadFourCC(r);
            int    chunkSize = r.ReadInt32();

            if (chunkId == "fmt ")
            {
                short audioFormat = r.ReadInt16();
                if (audioFormat != 1 && audioFormat != 3) // 1=PCM, 3=IEEE float
                    throw new InvalidDataException($"{sourceName}: unsupported audio format {audioFormat} (only PCM supported).");

                numChannels  = r.ReadInt16();
                sampleRate   = r.ReadInt32();
                r.ReadInt32(); // byte rate
                r.ReadInt16(); // block align
                bitsPerSample = r.ReadInt16();

                // Skip any extra fmt bytes
                int remaining = chunkSize - 16;
                if (remaining > 0) r.BaseStream.Seek(remaining, SeekOrigin.Current);
            }
            else if (chunkId == "data")
            {
                pcmData = r.ReadBytes(chunkSize);
                break;
            }
            else
            {
                // Unknown chunk — skip
                r.BaseStream.Seek(chunkSize, SeekOrigin.Current);
            }
        }

        if (pcmData == null)
            throw new InvalidDataException($"{sourceName}: no 'data' chunk found.");

        // ── Decode PCM samples → double[] (mono) ─────────────────────────────
        double[] samples = Decode(pcmData, numChannels, bitsPerSample);

        // ── Slice into frames ─────────────────────────────────────────────────
        if (samples.Length < FrameSize)
            throw new InvalidDataException(
                $"{sourceName}: only {samples.Length} samples; minimum {FrameSize} required for one frame.");

        int frameCount = samples.Length / FrameSize;
        var frames = new double[frameCount][];
        for (int f = 0; f < frameCount; f++)
        {
            frames[f] = new double[FrameSize];
            Array.Copy(samples, f * FrameSize, frames[f], 0, FrameSize);
        }
        return frames;
    }

    /// <summary>Decodes raw PCM bytes to a normalized [-1,1] double array (mono mixdown).</summary>
    private static double[] Decode(byte[] data, int channels, int bitsPerSample)
    {
        int bytesPerSample = bitsPerSample / 8;
        int totalSamples   = data.Length / (bytesPerSample * channels);
        var mono           = new double[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            double sum = 0.0;
            for (int ch = 0; ch < channels; ch++)
            {
                int byteOffset = (i * channels + ch) * bytesPerSample;
                sum += bitsPerSample switch
                {
                    8  => (data[byteOffset] - 128) / 128.0,
                    16 => (short)(data[byteOffset] | (data[byteOffset + 1] << 8)) / 32768.0,
                    24 => DecodeInt24(data, byteOffset) / 8388608.0,
                    32 => (int)(data[byteOffset] | (data[byteOffset + 1] << 8)
                              | (data[byteOffset + 2] << 16) | (data[byteOffset + 3] << 24))
                          / 2147483648.0,
                    _ => throw new InvalidDataException($"Unsupported bit depth: {bitsPerSample}")
                };
            }
            mono[i] = Math.Clamp(sum / channels, -1.0, 1.0);
        }
        return mono;
    }

    private static double DecodeInt24(byte[] data, int offset)
    {
        int raw = data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16);
        // Sign-extend from 24-bit
        if ((raw & 0x800000) != 0) raw |= unchecked((int)0xFF000000);
        return raw;
    }

    private static string ReadFourCC(BinaryReader r)
        => new string(r.ReadChars(4));
}
