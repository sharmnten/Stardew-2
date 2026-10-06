namespace StardewBrowser.Platform.Audio.Xact;

public class SoundEffect(BrowserWave wave) : IDisposable
{
    public BrowserWave Wave => wave;
    public string Name { get; set; } = $"{wave.Bank}/{wave.Track}";
    public TimeSpan Duration => TimeSpan.FromSeconds((double)wave.Samples / wave.Rate);
    public bool IsDisposed { get; private set; }
    public bool IsInUse => SoundEffectInstancePool._playingInstances.Any(instance => instance._effect == this);
    private static float masterVolume = 1;
    public static float MasterVolume
    {
        get => masterVolume;
        set
        {
            if (value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(value));
            masterVolume = value;
            SoundEffectInstancePool.UpdateMasterVolume();
        }
    }
    public static float DistanceScale { get; set; } = 1;
    public static float DopplerScale { get; set; } = 1;
    public static float SpeedOfSound { get; set; } = 343.5f;
    public static HashSet<SoundEffect> EffectsToRemove { get; } = [];
    public SoundEffectInstance CreateInstance() => new() { _effect = this, _isPooled = false };
    public virtual SoundEffectInstance GetPooledInstance(bool forXAct)
    {
        if (!SoundEffectInstancePool.SoundsAvailable) return null!;
        var instance = SoundEffectInstancePool.GetInstance(forXAct);
        instance._effect = this;
        return instance;
    }
    // Prepared vanilla bank effects are permanent, matching the original
    // _waveBankSound dependency exemption.
    public void AddDependency() { }
    public void RemoveDependency() { }
    public bool ShouldBeRemoved() => false;
    internal static void PlatformSetReverbSettings(ReverbSettings settings) => XactRuntime.Output.SetReverb(settings);
    public void Dispose()
    {
        if (IsDisposed) return;
        foreach (var instance in SoundEffectInstancePool._playingInstances.Where(instance => instance._effect == this).ToArray())
        {
            instance.Stop(true);
            instance.ReleaseToPool();
        }
        IsDisposed = true;
    }
}
