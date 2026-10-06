using StardewBrowser.Platform.Audio.Xact;
using Xunit;

public sealed class AudioStreamTests
{
    [Fact]
    public void FileBasedWaveKeepsOriginalRateChannelsAndDuration()
    {
        // RIFF PCM16 mono, 8000 Hz, four samples (zero, +32767, -32768, +1).
        byte[] wave = Convert.FromHexString("524946462c00000057415645666d74201000000001000100401f0000803e00000200100064617461080000000000ff7f00800100");
        using var effect = SoundEffect.FromStream(new MemoryStream(wave), false);
        Assert.Equal(8000, effect.Wave.Rate);
        Assert.Equal(1, effect.Wave.Channels);
        Assert.Equal(4, effect.Wave.Samples);
        Assert.Equal(TimeSpan.FromSeconds(4d / 8000), effect.Duration);
        Assert.Equal(wave, effect.Wave.EncodedData);
    }
}
