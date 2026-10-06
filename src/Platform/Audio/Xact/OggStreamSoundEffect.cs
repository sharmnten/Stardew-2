namespace StardewBrowser.Platform.Audio.Xact;

public sealed class OggStreamSoundEffect(string path) : SoundEffect(Read(path))
{
    private static BrowserWave Read(string path)
    {
        using var stream = File.OpenRead(path);
        return StreamWave.Read(stream, vorbis: true);
    }
}
