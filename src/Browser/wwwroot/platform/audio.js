(() => {
  let context, analyser, samples, peak = 0, downloadedBytes = 0, cue = null, error = null;
  const buffers = new Map(), voices = new Map();
  let scheduler = null, reverbSettings = null, reverb;
  const limit = 128 * 1024 * 1024;
  let residentBytes = 0;
  function status() {
    let currentLevel = 0;
    if (analyser) {
      analyser.getFloatTimeDomainData(samples);
      for (const sample of samples) currentLevel = Math.max(currentLevel, Math.abs(sample));
      peak = Math.max(peak, currentLevel);
    }
    return { audioCurrentLevel: currentLevel, audioState: context?.state ?? 'suspended', audioPeak: peak, audioCue: cue,
      audioError: error, audioScheduler: scheduler, audioActiveVoices: [...voices.values()].filter(voice => voice.state !== 2).length, audioDecodedBytes: residentBytes, audioDownloadedBytes: downloadedBytes };
  }
  window.portAudio = {
    async initialize() {
      context = new AudioContext();
      await context.suspend();
      analyser = context.createAnalyser();
      analyser.fftSize = 2048;
      analyser.connect(context.destination);
      samples = new Float32Array(analyser.fftSize);
    },
    async unlock() {
      if (!navigator.userActivation.hasBeenActive) throw new Error('Original audio requires a player gesture.');
      await context.resume();
    },
    status,
    hasWave(key) { return buffers.has(key); },
    async decode(key, data, descriptor) {
      // decodeAudioData normally resamples to the device rate. Decode at the
      // original rate first so sample counts and bank loop points stay exact.
      const decoder = new OfflineAudioContext(descriptor.channels, 1, descriptor.rate);
      const buffer = await decoder.decodeAudioData(data.slice().buffer);
      if (buffer.sampleRate !== descriptor.rate || buffer.length !== descriptor.samples ||
          buffer.numberOfChannels !== descriptor.channels) throw new Error(`Original audio format changed: ${key}`);
      const bytes = buffer.length * buffer.numberOfChannels * 4;
      if (bytes > limit) throw new Error(`Original wave exceeds the decoded audio cache: ${key}`);
      evictInactive(bytes);
      buffers.set(key, { buffer, bytes, active: 0, descriptor });
      residentBytes += bytes;
      downloadedBytes += data.length;
    },
    markScheduler() { scheduler = 'original-xact'; },
    markCue(name) { cue = name; peak = 0; },
    setReverb(settings) {
      if (reverbSettings?.every((value, index) => value === settings[index])) return;
      reverbSettings = settings;
      if (!reverb) {
        reverb = { input: context.createConvolver(), low: context.createBiquadFilter(),
          high: context.createBiquadFilter(), gain: context.createGain() };
        reverb.input.normalize = false;
        reverb.low.type = 'lowshelf';
        reverb.high.type = 'highshelf';
        reverb.input.connect(reverb.low).connect(reverb.high).connect(reverb.gain).connect(analyser);
      }
      const decay = Math.max(0.1, Math.min(10, settings[18]));
      const impulse = context.createBuffer(2, Math.ceil(context.sampleRate * decay), context.sampleRate);
      let seed = 137;
      for (let channel = 0; channel < 2; channel++) {
        const data = impulse.getChannelData(channel);
        const delay = Math.round(context.sampleRate * settings[1] / 1000);
        for (let i = delay; i < data.length; i++) {
          seed = (Math.imul(seed, 1664525) + 1013904223) | 0;
          const noise = (seed >>> 0) / 2147483648 - 1;
          data[i] = noise * 0.04 * (settings[19] / 100) * Math.exp(-6.907755 * (i - delay) / (context.sampleRate * decay));
        }
        const reflection = Math.min(data.length - 1, Math.round(context.sampleRate * settings[0] / 1000));
        data[reflection] += Math.min(Math.pow(10, settings[16] / 20), 3.16);
      }
      reverb.input.buffer = impulse;
      reverb.low.frequency.value = settings[9] * 50 + 50;
      reverb.low.gain.value = Math.min(settings[8] - 8, 0);
      reverb.high.frequency.value = Math.min(context.sampleRate / 2, settings[11] * 500 + 1000);
      reverb.high.gain.value = settings[10] - 8;
      reverb.gain.gain.value = Math.min(Math.pow(10, settings[17] / 20), 1) * settings[21] / 200;
    },
    createVoice(id, key, parameters) {
      const entry = buffers.get(key);
      if (!entry) throw new Error(`Original wave is not decoded: ${key}`);
      const voice = { entry, parameters, state: 0, progress: 0, clock: context.currentTime,
        source: null, gain: context.createGain(), pan: context.createStereoPanner(), filter: context.createBiquadFilter(), send: context.createGain(), routedFilter: null };
      voice.gain.connect(voice.pan).connect(analyser);
      if (reverb) voice.pan.connect(voice.send).connect(reverb.input);
      entry.active++;
      voices.set(id, voice);
      startVoice(voice);
    },
    updateVoice(id, parameters) {
      const voice = voices.get(id);
      if (!voice || voice.state === 2) return;
      advance(voice);
      voice.parameters = parameters;
      configure(voice);
      scheduleEnd(voice);
    },
    voiceStates() { return Object.fromEntries([...voices].map(([id, voice]) => [id, voice.state])); },
    pauseVoice(id) {
      const voice = voices.get(id);
      if (!voice || voice.state !== 0) return;
      advance(voice);
      detachSource(voice);
      voice.state = 1;
    },
    resumeVoice(id) {
      const voice = voices.get(id);
      if (!voice || voice.state !== 1) return;
      voice.state = 0;
      startVoice(voice);
    },
    stopVoice(id, immediate) {
      const voice = voices.get(id);
      if (!voice || voice.state === 2) return;
      if (immediate) finishVoice(voice);
      else {
        advance(voice);
        voice.parameters = { ...voice.parameters, loopCount: Math.floor(voice.progress / voice.entry.buffer.duration) };
        if (voice.state === 0) scheduleEnd(voice);
      }
    },
    destroyVoice(id) { const voice = voices.get(id); if (voice) finishVoice(voice); voices.delete(id); },
    fail(message) { error = message; },
    async dispose() { for (const voice of voices.values()) finishVoice(voice); voices.clear(); await context?.close(); buffers.clear(); residentBytes = 0; }
  };
  function advance(voice) {
    if (voice.state === 0 && voice.source) {
      voice.progress += (context.currentTime - voice.clock) * Math.pow(2, voice.parameters.pitch);
      voice.clock = context.currentTime;
    }
  }
  function evictInactive(requiredBytes = 0) {
    for (const [key, entry] of buffers) {
      if (residentBytes + requiredBytes <= limit) break;
      if (entry.active === 0) { buffers.delete(key); residentBytes -= entry.bytes; }
    }
  }
  function configure(voice) {
    const p = voice.parameters;
    voice.gain.gain.value = Math.max(0, p.volume);
    voice.pan.pan.value = Math.max(-1, Math.min(1, p.pan));
    voice.filter.type = ['lowpass', 'bandpass', 'highpass'][p.filterMode ?? 0];
    voice.filter.frequency.value = Math.min(context.sampleRate / 2, Math.max(0, p.filterFrequency ?? 20000));
    voice.filter.Q.value = p.filterQ ?? 1;
    voice.send.gain.value = Math.max(0, p.reverbMix ?? 0);
    if (voice.source) {
      voice.source.playbackRate.value = Math.pow(2, p.pitch);
      if (voice.routedFilter !== p.filterEnabled) {
        voice.source.disconnect();
        voice.filter.disconnect();
        if (p.filterEnabled) voice.source.connect(voice.filter).connect(voice.gain);
        else voice.source.connect(voice.gain);
        voice.routedFilter = p.filterEnabled;
      }
    }
  }
  function scheduleEnd(voice) {
    if (!voice.source || voice.state !== 0 || voice.parameters.loopCount >= 255) return;
    const remaining = voice.entry.buffer.duration * (voice.parameters.loopCount + 1) - voice.progress;
    voice.source.stop(context.currentTime + Math.max(0, remaining) / Math.pow(2, voice.parameters.pitch));
  }
  function startVoice(voice) {
    const source = context.createBufferSource();
    source.buffer = voice.entry.buffer;
    // The supplied native output loops the entire decoded Spring, ignoring
    // embedded loop regions. Those regions are retained in the manifest.
    source.loop = true;
    voice.source = source;
    voice.routedFilter = null;
    voice.clock = context.currentTime;
    configure(voice);
    source.onended = () => finishVoice(voice);
    source.start(0, voice.progress % source.buffer.duration);
    scheduleEnd(voice);
  }
  function detachSource(voice) {
    if (!voice.source) return;
    voice.source.onended = null;
    voice.source.stop();
    voice.source.disconnect();
    voice.filter.disconnect();
    voice.source = null;
  }
  function finishVoice(voice) {
    if (voice.state === 2) return;
    detachSource(voice);
    voice.state = 2;
    voice.entry.active--;
    evictInactive();
    voice.entry = null;
    voice.gain.disconnect();
    voice.pan.disconnect();
    voice.send.disconnect();
  }
  // Resume directly in the DOM gesture, before an asynchronous .NET callback.
  document.addEventListener('click', event => {
    if (event.target.closest('#audioProbe')) window.portAudio.unlock().catch(failure => window.portAudio.fail(failure.message));
  });
})();
