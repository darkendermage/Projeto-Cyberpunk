import { describe, expect, it } from 'vitest';
import { createSaveQueue, type SaveStatus } from './saveQueue';

const deferred = () => {
  let resolve!: () => void;
  let reject!: (e: unknown) => void;
  const promise = new Promise<void>((res, rej) => { resolve = res; reject = rej; });
  return { promise, resolve, reject };
};

describe('createSaveQueue', () => {
  it('keeps one save in flight and sends only the latest value pushed meanwhile', async () => {
    const calls: string[] = [];
    const gates: ReturnType<typeof deferred>[] = [];
    const statuses: SaveStatus[] = [];
    const queue = createSaveQueue<string>((v) => { calls.push(v); const g = deferred(); gates.push(g); return g.promise; }, (s) => statuses.push(s));

    const done = queue.push('a');
    void queue.push('b');
    void queue.push('c');
    expect(calls).toEqual(['a']);
    gates[0].resolve();
    await Promise.resolve(); await Promise.resolve();
    expect(calls).toEqual(['a', 'c']);
    gates[1].resolve();
    await done;
    expect(queue.busy()).toBe(false);
    expect(statuses.at(-1)).toBe('saved');
  });

  it('reports an error and keeps working for the next value', async () => {
    const statuses: SaveStatus[] = [];
    let fail = true;
    const queue = createSaveQueue<number>(async () => { if (fail) throw new Error('409'); }, (s) => statuses.push(s));
    await queue.push(1);
    expect(statuses.at(-1)).toBe('error');
    fail = false;
    await queue.push(2);
    expect(statuses.at(-1)).toBe('saved');
  });
});
