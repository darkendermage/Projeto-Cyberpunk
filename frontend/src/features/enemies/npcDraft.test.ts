import { describe, expect, it } from 'vitest';
import {
  buildGenerationRequest, cyberwareTree, descendants, draftReducer, emptySheet, mergeGenerated,
} from './npcDraft';

const withCyber = () => ({
  ...emptySheet('Boss'),
  cyberware: [
    { instanceId: 'nl', itemId: 1 },
    { instanceId: 'sock', itemId: 2, parentInstanceId: 'nl' },
    { instanceId: 'chip', itemId: 3, parentInstanceId: 'sock' },
    { instanceId: 'eye', itemId: 4 },
    { instanceId: 'scope', itemId: 5, parentInstanceId: 'eye' },
  ],
});

describe('draftReducer', () => {
  it('rounds and clamps STAT values', () => {
    let sheet = draftReducer(emptySheet(), { type: 'stat', key: 'REF', value: 6.6 });
    sheet = draftReducer(sheet, { type: 'stat', key: 'EMP', value: -3 });
    expect(sheet.stats.REF).toBe(7);
    expect(sheet.stats.EMP).toBe(0);
  });

  it('removes a skill when the value is cleared', () => {
    let sheet = draftReducer(emptySheet(), { type: 'skill', key: 'handgun', value: 5 });
    expect(sheet.skills.handgun).toBe(5);
    sheet = draftReducer(sheet, { type: 'skill', key: 'handgun', value: null });
    expect(sheet.skills).not.toHaveProperty('handgun');
  });

  it('toggles locks case-insensitively', () => {
    let sheet = draftReducer(emptySheet(), { type: 'toggleStatLock', key: 'REF' });
    expect(sheet.locks.stats).toEqual(['REF']);
    sheet = draftReducer(sheet, { type: 'toggleStatLock', key: 'ref' });
    expect(sheet.locks.stats).toEqual([]);
  });

  it('removing a foundational piece removes everything installed in it', () => {
    const sheet = draftReducer(withCyber(), { type: 'removeCyber', instanceId: 'nl' });
    expect(sheet.cyberware.map((c) => c.instanceId)).toEqual(['eye', 'scope']);
  });

  it('allowed state removes the cyberware rule', () => {
    let sheet = draftReducer(emptySheet(), { type: 'cyberRule', itemId: 9, state: 'Forbidden' });
    sheet = draftReducer(sheet, { type: 'cyberRule', itemId: 9, state: 'Required' });
    expect(sheet.cyberwareRules).toEqual([{ itemId: 9, state: 'Required' }]);
    sheet = draftReducer(sheet, { type: 'cyberRule', itemId: 9, state: 'Allowed' });
    expect(sheet.cyberwareRules).toEqual([]);
  });

  it('keeps one exception per issue key', () => {
    let sheet = draftReducer(emptySheet(), { type: 'addException', key: 'STAT_BUDGET_EXCEEDED', justification: ' a ' });
    sheet = draftReducer(sheet, { type: 'addException', key: 'STAT_BUDGET_EXCEEDED', justification: 'b' });
    expect(sheet.exceptions).toEqual([{ key: 'STAT_BUDGET_EXCEEDED', justification: 'b' }]);
  });
});

describe('cyberware tree', () => {
  it('lists descendants recursively', () => {
    expect(descendants(withCyber().cyberware, 'nl')).toEqual(['sock', 'chip']);
  });

  it('orders options under their foundational piece with depth', () => {
    expect(cyberwareTree(withCyber().cyberware).map((n) => `${n.entry.instanceId}:${n.depth}`))
      .toEqual(['nl:0', 'sock:1', 'chip:2', 'eye:0', 'scope:1']);
  });
});

describe('buildGenerationRequest', () => {
  const settings = { profileId: 'sniper', seed: ' 42 ', applyEmpGuideline: true };
  const sheet = { ...emptySheet('Lieutenant'), hardened: true, name: 'Cherub', cyberwareRules: [{ itemId: 3, state: 'Forbidden' as const }] };

  it('automatic mode sends only level and constraints', () => {
    const request = buildGenerationRequest('auto', sheet, settings);
    expect(request).toMatchObject({ level: 'Lieutenant', hardened: true, profileId: 'sniper', seed: 42, base: null, applyEmpGuideline: true });
    expect(request.cyberwareRules).toEqual([{ itemId: 3, state: 'Forbidden' }]);
  });

  it('semi-automatic mode sends the sheet with its locks', () => {
    const request = buildGenerationRequest('semi', sheet, { ...settings, profileId: '', seed: '' });
    expect(request.base?.name).toBe('Cherub');
    expect(request.profileId).toBeNull();
    expect(request.seed).toBeNull();
  });
});

describe('mergeGenerated', () => {
  it('keeps the user constraints and clears locks outside semi mode', () => {
    const current = { ...emptySheet(), cyberwareRules: [{ itemId: 1, state: 'Required' as const }], locks: { stats: ['REF'], skills: [], identity: true } };
    const generated = { ...emptySheet(), name: 'Gerado' };
    expect(mergeGenerated(generated, current, 'auto').locks.stats).toEqual([]);
    expect(mergeGenerated(generated, current, 'semi').locks.stats).toEqual(['REF']);
    expect(mergeGenerated(generated, current, 'auto').cyberwareRules).toEqual(current.cyberwareRules);
  });
});
