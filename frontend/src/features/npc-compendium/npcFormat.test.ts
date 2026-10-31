import { describe, expect, it } from 'vitest';
import { formatPrinted, weaponClassLabel } from './npcFormat';
import { t } from '../../i18n';

describe('formatPrinted', () => {
  it('shows a dash for null and undefined', () => {
    expect(formatPrinted(null)).toBe('—');
    expect(formatPrinted(undefined)).toBe('—');
  });
  it('keeps zero', () => {
    expect(formatPrinted(0)).toBe('0');
  });
  it('prints numbers', () => {
    expect(formatPrinted(17)).toBe('17');
  });
});

describe('weaponClassLabel', () => {
  it('names a WeaponClass like the rest of the app', () => {
    expect(weaponClassLabel('VeryHeavyPistol')).toBe(t('Pistola Muito Pesada', 'Very Heavy Pistol'));
    expect(weaponClassLabel('Smg')).toBe('SMG');
  });
  it('keeps a value it does not know', () => {
    expect(weaponClassLabel('Sling')).toBe('Sling');
  });
});
