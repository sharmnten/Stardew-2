using Microsoft.JSInterop;
using StardewBrowser.Platform.Audio.Xact;

namespace StardewBrowser.Platform.Audio;

public sealed class WebAudioOutput(IJSInProcessRuntime js, BrowserAudioAdapter adapter) : IXactAudioOutput
{
    private readonly Dictionary<int, WebVoice> voices = [];
    private int nextId;
    public IXactAudioVoice CreateVoice(BrowserWave wave)
    {
        var voice = new WebVoice(++nextId, wave, js, adapter);
        voices.Add(nextId, voice);
        return voice;
    }
    public void UpdateFrame()
    {
        var states = js.Invoke<Dictionary<int, SoundState>>("portAudio.voiceStates");
        foreach (var (id, voice) in voices.ToArray())
        {
            if (states.TryGetValue(id, out var state)) voice.UpdateState(state);
            if (voice.State != SoundState.Stopped) continue;
            voice.Dispose();
            voices.Remove(id);
        }
    }
    public void SetReverb(ReverbSettings settings)
    {
        // Keep all original XACT values available at the output boundary.
        js.InvokeVoid("portAudio.setReverb", Enumerable.Range(0, 22).Select(index => settings[index]).ToArray());
    }

    private sealed class WebVoice(int id, BrowserWave wave, IJSInProcessRuntime js, BrowserAudioAdapter adapter) : IXactAudioVoice
    {
        private VoiceParameters parameters = new();
        private bool loaded, disposed;
        public SoundState State { get; private set; } = SoundState.Stopped;
        public void Apply(VoiceParameters value)
        {
            parameters = value;
            if (loaded) js.InvokeVoid("portAudio.updateVoice", id, parameters);
        }
        public void Play()
        {
            if (disposed) throw new ObjectDisposedException(nameof(WebVoice));
            State = SoundState.Playing;
            _ = LoadAndPlayAsync();
        }
        private async Task LoadAndPlayAsync()
        {
            try
            {
                await adapter.PreloadWaveAsync(wave);
                if (disposed || State == SoundState.Stopped) return;
                js.InvokeVoid("portAudio.createVoice", id, $"{wave.Bank}/{wave.Track}", parameters);
                loaded = true;
                if (State == SoundState.Paused) js.InvokeVoid("portAudio.pauseVoice", id);
            }
            catch (Exception error)
            {
                State = SoundState.Stopped;
                js.InvokeVoid("portAudio.fail", error.Message);
            }
        }
        public void UpdateState(SoundState state) => State = state;
        public void Pause() { if (State == SoundState.Playing) { State = SoundState.Paused; if (loaded) js.InvokeVoid("portAudio.pauseVoice", id); } }
        public void Resume() { if (State == SoundState.Paused) { State = SoundState.Playing; if (loaded) js.InvokeVoid("portAudio.resumeVoice", id); } }
        public void Stop(bool immediate)
        {
            if (loaded) js.InvokeVoid("portAudio.stopVoice", id, immediate);
            if (immediate || !loaded) State = SoundState.Stopped;
        }
        public void Dispose()
        {
            if (disposed) return;
            Stop(true);
            if (loaded) js.InvokeVoid("portAudio.destroyVoice", id);
            disposed = true;
        }
    }
}
