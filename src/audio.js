// Sample decoding and pitch shifting are independent of the exercise engine.
export class Piano {
  context = null;
  buffers = new Map();
  active = [];
  async unlock() {
    this.context ??= new AudioContext();
    await this.context.resume();
    if (this.context.state !== 'running') throw new Error('Ο ήχος δεν ενεργοποιήθηκε.');
  }
  async loadSamples() {
    await this.unlock();
    const loaded = await Promise.all([48, 60, 72, 84].map(async midi => {
      const octave = midi / 12 - 1;
      const response = await fetch(`https://tonejs.github.io/audio/salamander/C${octave}.mp3`);
      if (!response.ok) throw new Error('Αποτυχία λήψης δειγμάτων');
      return [midi, await this.context.decodeAudioData(await response.arrayBuffer())];
    }));
    this.buffers = new Map(loaded); // Atomic switch; failed downloads preserve the current instrument.
  }
  stop() { for (const node of this.active) { try { node.stop(); } catch {} } this.active = []; }
  async play(exercise, volume = 0.4) {
    await this.unlock();
    this.stop();
    const now = this.context.currentTime + 0.05;
    exercise.notes.forEach((midi, index) => this.note(midi, now + (exercise.mode === 'harmonic' ? 0 : index * 0.9), volume));
  }
  note(midi, time, volume) {
    const ctx = this.context;
    const gain = ctx.createGain();
    gain.connect(ctx.destination);
    gain.gain.setValueAtTime(0, time);
    gain.gain.linearRampToValueAtTime(volume * 0.5, time + 0.008);
    gain.gain.exponentialRampToValueAtTime(0.001, time + 1.7);
    let source;
    if (this.buffers.size) {
      const nearest = [...this.buffers.keys()].sort((a, b) => Math.abs(a - midi) - Math.abs(b - midi))[0];
      source = ctx.createBufferSource();
      source.buffer = this.buffers.get(nearest);
      source.playbackRate.value = 2 ** ((midi - nearest) / 12);
    } else {
      source = ctx.createOscillator();
      source.type = 'triangle';
      source.frequency.value = 440 * 2 ** ((midi - 69) / 12);
    }
    source.connect(gain);
    source.start(time);
    source.stop(time + 1.8);
    this.active.push(source);
    source.onended = () => { source.disconnect(); gain.disconnect(); this.active = this.active.filter(n => n !== source); };
  }
}
