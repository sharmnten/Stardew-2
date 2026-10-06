namespace StardewBrowser.Platform.Audio.Xact;

// Original bank decoding is performed and verified by build_audio.py. Runtime
// construction creates descriptors only; the output loads individual waves.
public sealed class WaveBank : IDisposable
{
    private readonly AudioEngine engine;
    private readonly SoundEffect[] sounds;
    public string Name { get; }
    public bool IsPrepared => !IsDisposed;
    public bool IsDisposed { get; private set; }
    public bool IsInUse => sounds.Any(sound => sound.IsInUse);
    public WaveBank(AudioEngine audioEngine, string fileName) : this(audioEngine, fileName, 0, 0) { }
    public WaveBank(AudioEngine audioEngine, string fileName, int offset, short packetsize)
    {
        ArgumentNullException.ThrowIfNull(audioEngine);
        if (offset != 0) throw new ArgumentOutOfRangeException(nameof(offset), "Prepared original banks have no embedded offset.");
        engine = audioEngine;
        Name = Path.GetFileNameWithoutExtension(fileName.Replace('\\', '/'));
        var waves = XactRuntime.GetBank(Name);
        if (waves.Where((wave, index) => wave.Track != index).Any()) throw new InvalidDataException("Original wave bank tracks are not contiguous: " + Name);
        sounds = waves.Select(wave => new SoundEffect(wave)).ToArray();
        engine.Wavebanks.Add(Name, this);
    }
    public SoundEffect GetSoundEffect(int track) => sounds[track];
    public SoundEffectInstance GetSoundEffectInstance(int track, out bool streaming)
    {
        streaming = false; // Both bank forms use the same lazy browser output.
        return sounds[track].GetPooledInstance(true);
    }
    public void Dispose()
    {
        if (IsDisposed) return;
        foreach (var sound in sounds) sound.Dispose();
        engine.Wavebanks.Remove(Name);
        IsDisposed = true;
    }
}
