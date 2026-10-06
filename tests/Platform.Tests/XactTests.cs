using System.Text.Json;
using StardewBrowser.Platform.Audio;
using StardewBrowser.Platform.Audio.Xact;
using Xunit;

public class XactTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static (AudioEngine engine, SoundBank bank, RecordingOutput output) Open()
    {
        var manifest = JsonSerializer.Deserialize<AudioManifest>(File.ReadAllText(Fixture("manifest.json")), Json)!;
        var output = new RecordingOutput();
        XactRuntime.Configure(path => File.OpenRead(Fixture(Path.GetFileName(path.Replace('\\', '/')))), manifest, output);
        var engine = new AudioEngine("Content/XACT/FarmerSounds.xgs");
        _ = new WaveBank(engine, "Content/XACT/Wave Bank.xwb");
        _ = new WaveBank(engine, "Content/XACT/Wave Bank(1.4).xwb");
        return (engine, new SoundBank(engine, "Content/XACT/Sound Bank.xsb"), output);
    }

    [Fact]
    public void OriginalParserAndLazyBanksBindEveryOriginalCueAndWave()
    {
        var (engine, bank, output) = Open();
        using (engine)
        using (bank)
        using (var metadata = JsonDocument.Parse(File.ReadAllText(Fixture("cues.json"))))
        {
            var bound = new HashSet<(int, int)>();
            var definitions = metadata.RootElement.GetProperty("cues").EnumerateObject().ToArray();
            Assert.Equal(433, definitions.Length);
            foreach (var definition in definitions)
            {
                Assert.True(bank.Exists(definition.Name), definition.Name);
                foreach (var sound in bank.GetCueDefinition(definition.Name).sounds)
                {
                    if (!sound.complexSound) Add(bank.GetSoundEffect(sound.waveBankIndex, sound.trackIndex));
                    else foreach (var clip in sound.soundClips)
                        foreach (var wave in clip.clipEvents.OfType<PlayWaveEvent>())
                            foreach (var variant in wave.GetVariants()) Add(variant.GetSoundEffect());
                }
            }
            Assert.Equal(463, bound.Count);
            Assert.Empty(output.Voices); // Parsing does not decode or play any wave.
            void Add(SoundEffect effect) => bound.Add((effect.Wave.Bank, effect.Wave.Track));
        }
    }

    [Fact]
    public void OriginalCueLimitAndCategoryVolumeControlTheOutput()
    {
        var (engine, bank, output) = Open();
        using (engine)
        using (bank)
        {
            engine.GetCategory("Sound").SetVolume(0.25f);
            var first = bank.GetCue("bigSelect");
            first.Play();
            var voice = Assert.Single(output.Voices);
            Assert.Equal((0, 3), (voice.Wave.Bank, voice.Wave.Track));
            Assert.Equal(0.251021275f, voice.Parameters.Volume, 6);
            var second = bank.GetCue("bigSelect");
            second.Play();
            Assert.False(second.IsPlaying); // Original limit is one, FailToPlay.
            first.Pause();
            Assert.True(first.IsPaused);
            first.Resume();
            Assert.Equal(SoundState.Playing, voice.State);
            first.Stop(AudioStopOptions.Immediate);
            Assert.Equal(SoundState.Stopped, voice.State);
            second.Play();
            Assert.True(second.IsPlaying);
            second.Stop(AudioStopOptions.Immediate);
        }
    }

    [Fact]
    public void FinishedStandaloneVoicesReturnToThePoolWithoutExhaustingTheLimit()
    {
        var (engine, bank, output) = Open();
        using (engine)
        using (bank)
        {
            var effect = bank.GetSoundEffect(0, 3);
            for (int i = 0; i < 300; i++)
            {
                var instance = effect.GetPooledInstance(false);
                Assert.NotNull(instance);
                instance.Play();
                output.Voices[^1].Stop(true); // The sample finishes at the output boundary.
                XactRuntime.UpdateVoices();
            }
            Assert.Equal(300, output.Voices.Count);
        }
    }

    [Fact]
    public void StandaloneVolumeTracksMasterVolumeWithoutChangingXactCategoryVolume()
    {
        var (engine, bank, output) = Open();
        using (engine)
        using (bank)
        {
            try
            {
                SoundEffect.MasterVolume = 0.5f;
                var instance = bank.GetSoundEffect(0, 3).CreateInstance();
                instance.Play();
                var voice = Assert.Single(output.Voices);
                Assert.Equal(0.5f, voice.Parameters.Volume);
                instance.Volume = 0.25f;
                Assert.Equal(0.125f, voice.Parameters.Volume);
                SoundEffect.MasterVolume = 0.25f;
                Assert.Equal(0.0625f, voice.Parameters.Volume);
                Assert.Throws<ArgumentOutOfRangeException>(() => instance.Pan = 2);
                Assert.Throws<ArgumentOutOfRangeException>(() => instance.Pitch = 2);
                Assert.Throws<ArgumentOutOfRangeException>(() => instance.Volume = 2);
                instance.Dispose();
            }
            finally { SoundEffect.MasterVolume = 1; }
        }
    }

    [Fact]
    public void OriginalToolChargeRpcMapsChargeToPitch()
    {
        var (engine, bank, output) = Open();
        using (engine)
        using (bank)
        {
            var cue = bank.GetCue("toolCharge");
            cue.SetVariable("Pitch", 1200); // Original second RPC is neutral at 1200.
            cue.SetVariable("Charge", 0);
            cue.Play();
            var voice = Assert.Single(output.Voices);
            Assert.Equal(-1, voice.Parameters.Pitch, 5);
            cue.SetVariable("Charge", 24);
            engine.Update();
            Assert.Equal(0, voice.Parameters.Pitch, 5);
            cue.SetVariable("Charge", 48);
            engine.Update();
            Assert.Equal(1, voice.Parameters.Pitch, 5);
            cue.Stop(AudioStopOptions.Immediate);
        }
    }

    private sealed class RecordingOutput : IXactAudioOutput
    {
        public List<RecordingVoice> Voices { get; } = [];
        public IXactAudioVoice CreateVoice(BrowserWave wave)
        {
            var voice = new RecordingVoice(wave);
            Voices.Add(voice);
            return voice;
        }
        public void SetReverb(ReverbSettings settings) { }
    }

    private sealed class RecordingVoice(BrowserWave wave) : IXactAudioVoice
    {
        public BrowserWave Wave => wave;
        public SoundState State { get; private set; }
        public VoiceParameters Parameters { get; private set; } = new();
        public void Apply(VoiceParameters parameters) => Parameters = parameters;
        public void Play() => State = SoundState.Playing;
        public void Pause() => State = SoundState.Paused;
        public void Resume() => State = SoundState.Playing;
        public void Stop(bool immediate) => State = SoundState.Stopped;
        public void Dispose() => Stop(true);
    }
}
