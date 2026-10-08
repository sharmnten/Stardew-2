using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using StardewBrowser.Platform.Content;
using StardewBrowser.Platform.Audio.Xact;

namespace StardewBrowser.Platform.Audio;

public sealed record WaveLoops(
    [property: JsonPropertyName("loop_start")] int Start,
    [property: JsonPropertyName("loop_length")] int Length);
public sealed record BrowserWave(int Bank, int Track, string Path, string Sha256, int Size,
    int Rate, int Channels, int Samples, WaveLoops Original,
    [property: JsonPropertyName("bank_name")] string BankName,
    [property: JsonIgnore] byte[]? EncodedData = null);
public sealed record AudioManifest(int Schema,
    [property: JsonPropertyName("game_version")] string GameVersion,
    [property: JsonPropertyName("archive_sha256")] string ArchiveSha256,
    [property: JsonPropertyName("cue_count")] int CueCount,
    [property: JsonPropertyName("metadata_path")] string MetadataPath,
    [property: JsonPropertyName("metadata_sha256")] string MetadataSha256,
    BrowserWave[] Waves);

// The output boundary only. Original XACT scheduling is adapted separately.
public sealed class BrowserAudioAdapter(IJSRuntime js, HttpClient http) : IAsyncDisposable
{
    private readonly Dictionary<(int, int), BrowserWave> waves = [];
    private readonly SemaphoreSlim loading = new(1, 1);
    private JsonDocument? cues;
    private AudioManifest manifest = null!;
    private AudioEngine? engine;
    private SoundBank? bank;
    private WebAudioOutput? output;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        manifest = await StaticAssetDownload.GetJsonAsync<AudioManifest>(http, "Audio/manifest.json", cancellationToken)
            ?? throw new InvalidDataException("The original audio manifest is empty.");
        if (manifest.Schema != 1 || manifest.GameVersion != "1.6.15.24356" ||
            manifest.ArchiveSha256 != "fb0155d3efb94fdcda1f26ee1b048898fd568257732b4e47ee15e03f266cca11" ||
            manifest.CueCount != 433 || manifest.Waves.Length != 463 || manifest.MetadataPath != "Audio/cues.json")
            throw new InvalidDataException("Audio manifest does not match the original game.");
        foreach (var wave in manifest.Waves)
        {
            if (wave.Bank is < 0 or > 1 || wave.Track < 0 || wave.Path != $"Audio/{wave.Bank}/{wave.Track:000}.flac" ||
                wave.Rate <= 0 || wave.Channels is < 1 or > 2 || wave.Samples <= 0 || wave.Size <= 0 ||
                wave.Sha256.Length != 64 || wave.Original.Start < 0 || wave.Original.Length < 0 ||
                (long)wave.Original.Start + wave.Original.Length > wave.Samples || !waves.TryAdd((wave.Bank, wave.Track), wave))
                throw new InvalidDataException("Invalid original wave descriptor: " + wave.Path);
        }
        byte[] metadata = await StaticAssetDownload.GetBytesAsync(http, manifest.MetadataPath, cancellationToken);
        Verify(metadata, manifest.MetadataSha256, "cue metadata");
        cues = JsonDocument.Parse(metadata);
        if (cues.RootElement.GetProperty("cueCount").GetInt32() != manifest.CueCount)
            throw new InvalidDataException("Original cue count changed.");
        await js.InvokeVoidAsync("portAudio.initialize", cancellationToken);
    }

    public Task UnlockAsync() => js.InvokeVoidAsync("portAudio.unlock").AsTask();

    public async Task InitializeXactAsync(BrowserContentStore content, CancellationToken cancellationToken)
    {
        await ConfigureXactAsync(content, cancellationToken);
        engine = new AudioEngine("Content/XACT/FarmerSounds.xgs");
        _ = new WaveBank(engine, "Content/XACT/Wave Bank.xwb");
        _ = new WaveBank(engine, "Content/XACT/Wave Bank(1.4).xwb");
        bank = new SoundBank(engine, "Content/XACT/Sound Bank.xsb");
    }

    public async Task ConfigureXactAsync(BrowserContentStore content, CancellationToken cancellationToken)
    {
        await content.PreloadAsync(["XACT/FarmerSounds.xgs", "XACT/Sound Bank.xsb"], cancellationToken);
        output = new WebAudioOutput((IJSInProcessRuntime)js, this);
        XactRuntime.Configure(path => content.Open(path.Replace('\\', '/').Replace("Content/", "")), manifest, output);
        ((IJSInProcessRuntime)js).InvokeVoid("portAudio.markScheduler");
    }

    public void UpdateFrame()
    {
        output?.UpdateFrame();
        engine?.Update();
        XactRuntime.UpdateVoices();
    }

    public async Task PreloadWaveAsync(int bank, int track, CancellationToken cancellationToken = default)
    {
        if (!waves.TryGetValue((bank, track), out var wave)) throw new FileNotFoundException($"Missing original wave: {bank}/{track}");
        await PreloadWaveAsync(wave, cancellationToken);
    }

    public async Task PreloadWaveAsync(BrowserWave wave, CancellationToken cancellationToken = default)
    {
        await loading.WaitAsync(cancellationToken);
        try
        {
            string key = $"{wave.Bank}/{wave.Track}";
            if (await js.InvokeAsync<bool>("portAudio.hasWave", cancellationToken, key)) return;
            byte[] data = wave.EncodedData ?? await StaticAssetDownload.GetBytesAsync(http, wave.Path, cancellationToken);
            if (data.Length != wave.Size) throw new InvalidDataException("Original audio checksum mismatch: " + key);
            Verify(data, wave.Sha256, key);
            await js.InvokeVoidAsync("portAudio.decode", cancellationToken, key, data, wave);
        }
        finally { loading.Release(); }
    }

    public async Task PlayProbeAsync()
    {
        try
        {
            var sound = cues!.RootElement.GetProperty("cues").GetProperty("bigSelect")
                .GetProperty("fields").GetProperty("sounds")[0].GetProperty("fields");
            int bank = sound.GetProperty("waveBankIndex").GetInt32(), track = sound.GetProperty("trackIndex").GetInt32();
            await PreloadWaveAsync(bank, track);
            this.bank!.GetCue("bigSelect").Play();
            await js.InvokeVoidAsync("portAudio.markCue", "bigSelect");
        }
        catch (Exception error) { await js.InvokeVoidAsync("portAudio.fail", error.Message); }
    }

    private static void Verify(byte[] data, string sha256, string name)
    {
        if (!Convert.ToHexString(SHA256.HashData(data)).Equals(sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Original audio checksum mismatch: " + name);
    }

    public async ValueTask DisposeAsync()
    {
        cues?.Dispose();
        bank?.Dispose();
        engine?.Dispose();
        await js.InvokeVoidAsync("portAudio.dispose");
    }
}
