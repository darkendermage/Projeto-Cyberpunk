import type { EffectKind } from './board/effects';

// Generated with Web Audio, no sound files: noise bursts for shots, a rumble for explosions, a hiss for blades,
// a click for dice.
let context: AudioContext | null = null;

function audio(): AudioContext | null {
  if (typeof window === 'undefined' || !('AudioContext' in window)) return null;
  context ??= new AudioContext();
  if (context.state === 'suspended') void context.resume();
  return context;
}

function noise(ctx: AudioContext, seconds: number): AudioBuffer {
  const buffer = ctx.createBuffer(1, Math.ceil(ctx.sampleRate * seconds), ctx.sampleRate);
  const data = buffer.getChannelData(0);
  for (let i = 0; i < data.length; i++) data[i] = Math.random() * 2 - 1;
  return buffer;
}

/** A burst of noise through a filter, with a sharp attack and a decay. */
function burst(ctx: AudioContext, at: number, { seconds, type, frequency, q = 1, volume }: { seconds: number; type: BiquadFilterType; frequency: number; q?: number; volume: number }) {
  const source = ctx.createBufferSource();
  source.buffer = noise(ctx, seconds);
  const filter = ctx.createBiquadFilter();
  filter.type = type;
  filter.frequency.value = frequency;
  filter.Q.value = q;
  const gain = ctx.createGain();
  gain.gain.setValueAtTime(0.0001, at);
  gain.gain.exponentialRampToValueAtTime(volume, at + 0.005);
  gain.gain.exponentialRampToValueAtTime(0.0001, at + seconds);
  source.connect(filter).connect(gain).connect(ctx.destination);
  source.start(at);
  source.stop(at + seconds + 0.05);
}

export function playAttack(kind: EffectKind, volume = 0.5) {
  const ctx = audio();
  if (!ctx) return;
  const now = ctx.currentTime;
  switch (kind) {
    case 'shot':
      burst(ctx, now, { seconds: 0.25, type: 'lowpass', frequency: 1800, volume: volume });
      break;
    case 'burst':
      for (let i = 0; i < 4; i++) burst(ctx, now + i * 0.085, { seconds: 0.16, type: 'lowpass', frequency: 2000, volume: volume * 0.8 });
      break;
    case 'melee':
      burst(ctx, now, { seconds: 0.22, type: 'bandpass', frequency: 4200, q: 3, volume: volume * 0.6 });
      break;
    case 'blast':
      burst(ctx, now, { seconds: 1.2, type: 'lowpass', frequency: 380, volume: volume * 1.4 });
      break;
  }
}

/** Dice landing: a few short clicks. */
export function playDice(count: number, volume = 0.35) {
  const ctx = audio();
  if (!ctx) return;
  const now = ctx.currentTime;
  for (let i = 0; i < Math.min(6, count + 2); i++) burst(ctx, now + 0.05 + i * 0.07 + Math.random() * 0.03, { seconds: 0.04, type: 'highpass', frequency: 2500, volume });
}
