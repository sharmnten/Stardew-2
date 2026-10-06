namespace StardewBrowser.Platform.Audio.Xact;

public sealed record VoiceParameters(float Volume = 1, float Pitch = 0, float Pan = 0,
    uint LoopCount = 0, float ReverbMix = 0, bool FilterEnabled = false,
    FilterMode FilterMode = FilterMode.LowPass, float FilterQ = 1, float FilterFrequency = 20000);

public interface IXactAudioVoice : IDisposable
{
    SoundState State { get; }
    void Apply(VoiceParameters parameters);
    void Play();
    void Pause();
    void Resume();
    void Stop(bool immediate);
}

public interface IXactAudioOutput
{
    IXactAudioVoice CreateVoice(BrowserWave wave);
    void SetReverb(ReverbSettings settings);
}

public static class XactRuntime
{
    private static Func<string, Stream> open = _ => throw new InvalidOperationException("Original audio content has not been configured.");
    private static Dictionary<string, BrowserWave[]> banks = [];
    internal static IXactAudioOutput Output { get; private set; } = null!;

    public static void Configure(Func<string, Stream> openStream, AudioManifest manifest, IXactAudioOutput output)
    {
        open = openStream;
        Output = output;
        banks = manifest.Waves.GroupBy(wave => wave.BankName)
            .ToDictionary(group => group.Key, group => group.OrderBy(wave => wave.Track).ToArray());
    }

    internal static Stream OpenStream(string path) => open(path);
    public static void UpdateVoices()
    {
        // Replaces the desktop OpenAL manager thread. XACT owns its active
        // cues; unowned finished voices return to the original instance pool.
        foreach (var instance in SoundEffectInstancePool._playingInstances.ToArray())
            if (!instance._isXAct && (instance.IsDisposed || instance.State == SoundState.Stopped))
                instance.ReleaseToPool();
    }
    internal static BrowserWave[] GetBank(string name) => banks.TryGetValue(name, out var bank)
        ? bank : throw new FileNotFoundException("Missing prepared original wave bank: " + name);
    internal static void Raise(object sender, EventHandler<EventArgs>? handler, EventArgs args) => handler?.Invoke(sender, args);
}
