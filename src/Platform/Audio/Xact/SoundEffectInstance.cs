namespace StardewBrowser.Platform.Audio.Xact;

public class SoundEffectInstance : IDisposable
{
    internal bool _isPooled = true;
    public bool _isXAct;
    internal SoundEffect _effect = null!;
    private IXactAudioVoice? voice;
    private VoiceParameters parameters = new();
    private bool registered;
    public bool IsDisposed { get; private set; }
    public virtual uint LoopCount { get => parameters.LoopCount; set => Apply(parameters with { LoopCount = value }); }
    public float Volume
    {
        get => parameters.Volume;
        set { if (!_isXAct && (value < 0 || value > 1)) throw new ArgumentOutOfRangeException(nameof(value)); Apply(parameters with { Volume = value }); }
    }
    public float Pitch
    {
        get => parameters.Pitch;
        set { if (!_isXAct && (value < -1 || value > 1)) throw new ArgumentOutOfRangeException(nameof(value)); Apply(parameters with { Pitch = value }); }
    }
    public float Pan
    {
        get => parameters.Pan;
        set { if (value < -1 || value > 1) throw new ArgumentOutOfRangeException(nameof(value)); Apply(parameters with { Pan = value }); }
    }
    public virtual SoundState State => voice?.State ?? SoundState.Stopped;
    internal FilterMode _filterMode => parameters.FilterMode;
    internal float _filterQ => parameters.FilterQ;
    internal float _filterFrequency => parameters.FilterFrequency;
    private void Apply(VoiceParameters value)
    {
        parameters = value;
        voice?.Apply(_isXAct ? value : value with { Volume = value.Volume * SoundEffect.MasterVolume });
    }
    public virtual void Play()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (State == SoundState.Playing) return;
        if (State == SoundState.Paused) { Resume(); return; }
        if (!SoundEffectInstancePool.SoundsAvailable) throw new InstancePlayLimitException();
        voice?.Dispose();
        voice = XactRuntime.Output.CreateVoice(_effect.Wave);
        voice.Apply(_isXAct ? parameters : parameters with { Volume = Volume * SoundEffect.MasterVolume });
        voice.Play();
        SoundEffectInstancePool.Remove(this);
        registered = true;
    }
    public virtual void Pause() => voice?.Pause();
    public virtual void Resume() => voice?.Resume();
    public virtual void Stop() => Stop(true);
    public virtual void Stop(bool immediate)
    {
        voice?.Stop(immediate);
    }
    internal void ReleaseToPool()
    {
        if (!registered) return;
        registered = false;
        voice?.Dispose();
        voice = null;
        SoundEffectInstancePool.Add(this);
    }
    internal void PlatformSetReverbMix(float mix) => Apply(parameters with { ReverbMix = mix });
    internal void PlatformSetFilter(FilterMode mode, float q, float frequency) =>
        Apply(parameters with { FilterEnabled = true, FilterMode = mode, FilterQ = q, FilterFrequency = frequency });
    internal bool IsFilterEnabled() => parameters.FilterEnabled;
    internal void PlatformClearFilter() => Apply(parameters with { FilterEnabled = false });
    public void Dispose()
    {
        if (IsDisposed) return;
        Stop(true);
        voice?.Dispose();
        ReleaseToPool();
        IsDisposed = true;
    }
}

// Preserves the original Cue type check for optional streamed overrides.
// Prepared original banks use the common lazy output above.
public class OggStreamSoundEffectInstance : SoundEffectInstance { }
